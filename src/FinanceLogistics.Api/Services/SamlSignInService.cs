using System.Data;
using System.Security.Cryptography;
using System.Text;
using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Data;
using FinanceLogistics.Api.Models;
using FinanceLogistics.Api.Security;
using ITfoxtec.Identity.Saml2;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceLogistics.Api.Services;

public sealed record SamlStartResult(
    Saml2AuthnRequest Request,
    string BrowserSecret,
    DateTime ExpiresAtUtc);

public sealed class SamlSignInService(
    IdentityDbContext db,
    AuthService auth,
    SamlProtocol protocol,
    IdentitySettings settings,
    TimeProvider clock)
{
    public async Task<SamlStartResult> StartAsync(
        string? previousCookie,
        CancellationToken ct)
    {
        var request = protocol.CreateRequest();
        var secret = SamlBrowserState.CreateSecret();
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.Saml.RequestMinutes);

        if (string.IsNullOrWhiteSpace(request.IdAsString) ||
            request.IdAsString.Length > 128)
        {
            throw new InvalidOperationException("Invalid SAML request ID.");
        }

        db.SamlLoginRequests.Add(new()
        {
            RequestId = request.IdAsString,
            BrowserSecretHash = SamlBrowserState.Hash(secret),
            CreatedAtUtc = now,
            ExpiresAtUtc = expires,
            PreviousSessionId =
                await auth.ResolvePreviousSessionAsync(previousCookie, ct)
        });

        await db.SaveChangesAsync(ct);

        return new(request, secret, expires);
    }

    public async Task<LoginResult> CompleteAsync(
        HttpRequest request,
        string requestId,
        string encodedResponse,
        string? browserSecret,
        string correlationId,
        CancellationToken ct)
    {
        var pending = await db.SamlLoginRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestId == requestId, ct);

        var now = clock.GetUtcNow().UtcDateTime;

        if (pending is null ||
            pending.RequestId != requestId ||
            pending.ConsumedAtUtc is not null ||
            pending.ExpiresAtUtc <= now ||
            !SamlBrowserState.Matches(
                browserSecret, pending.BrowserSecretHash))
        {
            throw InvalidRequest();
        }

        var verified = protocol.Verify(
            request, encodedResponse, pending.RequestId);

        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();

                await using var transaction =
                    await db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable, ct);

                var consumedAt = clock.GetUtcNow().UtcDateTime;

                if (verified.ExpiresAtUtc <= consumedAt)
                    throw InvalidRequest();

                var updated = await db.SamlLoginRequests
                    .Where(x =>
                        x.RequestId == pending.RequestId &&
                        x.ConsumedAtUtc == null &&
                        x.ExpiresAtUtc > consumedAt)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            x => x.ConsumedAtUtc,
                            (DateTime?)consumedAt),
                        ct);

                if (updated != 1)
                    throw InvalidRequest();

                var identity = await db.ExternalIdentities.AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.ProviderKey == settings.Saml.ProviderKey &&
                        x.TenantId == verified.TenantId &&
                        x.ObjectId == verified.ObjectId &&
                        x.IsActive,
                        ct);

                if (identity is null)
                {
                    throw new IdentityFault(
                        403,
                        "SAML_ACCOUNT_NOT_LINKED",
                        "Tài khoản SSO chưa được liên kết hoặc đã bị khóa. Liên hệ IT.");
                }

                db.SamlReplayProofs.AddRange(
                    Proof("response", verified.ResponseId, verified.ExpiresAtUtc),
                    Proof("assertion", verified.AssertionId, verified.ExpiresAtUtc));

                await db.SaveChangesAsync(ct);

                // IdentityStore dùng cùng scoped IdentityDbContext.
                // SaveChanges của nó tham gia transaction hiện tại.
                var result = await auth.LoginSamlAsync(
                    identity,
                    pending.PreviousSessionId,
                    correlationId,
                    ct);

                await transaction.CommitAsync(ct);

                return result;
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number is 2601 or 2627)
        {
            throw InvalidRequest();
        }
    }

    private SamlReplayProof Proof(
        string kind,
        string id,
        DateTime expires)
    {
        var input = $"{settings.Saml.IdpIssuer}\n{kind}\n{id}";

        return new()
        {
            ProofHash = SHA256.HashData(Encoding.UTF8.GetBytes(input)),
            ExpiresAtUtc = expires.AddMinutes(2)
        };
    }

    private static IdentityFault InvalidRequest() =>
        new(
            400,
            "SAML_REQUEST_INVALID",
            "Yêu cầu SSO đã hết hạn, đã được sử dụng hoặc không thuộc trình duyệt này.");
}