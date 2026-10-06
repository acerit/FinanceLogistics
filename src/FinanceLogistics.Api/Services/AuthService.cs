using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Models;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;

namespace FinanceLogistics.Api.Services;

public sealed record LoginResult(AuthResponse Response, string SessionCookie);

public sealed class AuthService(IIdentityStore store, IAdAuthenticator directory,
    TokenService tokens, IdentitySettings settings, TimeProvider clock)
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, string? previousCookie, string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            throw new IdentityFault(400, "INVALID_REQUEST", "Vui lòng nhập tài khoản và mật khẩu.");
        var user = await store.FindUserByAliasAsync(AccessRules.NormalizeAlias(request.Username), ct);
        if (user is null || !directory.Validate(user, request.Password))
        {
            await store.AuditFailureAsync(user?.UserId, "LOGIN_FAILED", correlationId, ct);
            throw IdentityFault.InvalidLogin();
        }
        // Re-read active status/permissions after directory validation.
        user = await store.FindUserAsync(user.UserId, ct) ?? throw IdentityFault.InvalidLogin();
        var profile = AccessRules.Profile(user, await store.ActiveSitesAsync(ct));
        if (profile is null)
        {
            await store.AuditFailureAsync(user.UserId, "LOGIN_ACCESS_DENIED", correlationId, ct);
            throw IdentityFault.AccessDisabled();
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var session = new AuthSession
        {
            SessionId = Guid.NewGuid(), UserId = user.UserId, CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(settings.SessionMinutes)
        };
        var secret = SessionSecrets.Create(session.SessionId);
        session.SecretHash = secret.Hash;
        var previous = await FindCookieSessionAsync(previousCookie, ct);
        await store.StartSessionAsync(session, previous?.SessionId, correlationId, ct);
        return new(tokens.Issue(profile, session), secret.Cookie);
    }

    public async Task<AuthResponse> ResumeAsync(string? cookie, CancellationToken ct)
    {
        var session = await FindCookieSessionAsync(cookie, ct) ?? throw IdentityFault.SessionExpired();
        EnsureSessionActive(session);
        var profile = AccessRules.Profile(session.User, await store.ActiveSitesAsync(ct)) ?? throw IdentityFault.AccessDisabled();
        return tokens.Issue(profile, session);
    }

    public async Task LogoutAsync(string? cookie, string correlationId, CancellationToken ct)
    {
        var session = await FindCookieSessionAsync(cookie, ct);
        if (session is not null) await store.RevokeSessionAsync(session.SessionId, correlationId, ct);
    }

    public async Task<UserProfile?> ValidateAccessAsync(Guid sessionId, string subject, CancellationToken ct)
    {
        var session = await store.FindSessionAsync(sessionId, ct);
        if (session is null || session.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture) != subject
            || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) return null;
        return AccessRules.Profile(session.User, await store.ActiveSitesAsync(ct));
    }

    private void EnsureSessionActive(AuthSession session)
    {
        if (session.RevokedAtUtc is not null || session.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            throw IdentityFault.SessionExpired();
    }
    private async Task<AuthSession?> FindCookieSessionAsync(string? cookie, CancellationToken ct)
    {
        if (!SessionSecrets.TryRead(cookie, out var id, out var hash)) return null;
        var session = await store.FindSessionAsync(id, ct);
        return session is not null && SessionSecrets.Matches(session.SecretHash, hash) ? session : null;
    }
}
