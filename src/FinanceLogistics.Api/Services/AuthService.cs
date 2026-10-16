using System.Globalization;
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
        if (!settings.LdapEnabled)
        {
            throw new IdentityFault(403, "LDAP_SIGNIN_DISABLED", "Vui lòng đăng nhập bằng SSO.");
        }
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            throw new IdentityFault(400, "INVALID_REQUEST", "Vui lòng nhập tài khoản và mật khẩu.");
        }
        var user = await store.FindUserByAliasAsync(AccessRules.NormalizeAlias(request.Username), ct);
        if (user?.DirectoryAccount is null || !directory.Validate(user, request.Password))
        {
            await store.AuditFailureAsync(user?.UserId, "LOGIN_FAILED", correlationId, ct);
            throw IdentityFault.InvalidLogin();
        }
        var previousId = await ResolvePreviousSessionAsync(previousCookie, ct);
        return await StartSessionAsync(user.UserId, "LDAP", null, previousId, correlationId, ct);
    }

    // Chỉ được gọi từ luồng SAML phía server sau khi xác minh phản hồi.
    internal Task<LoginResult> LoginSamlAsync(ExternalIdentity identity, Guid? previousId, string correlationId, CancellationToken ct)
    {
        if (!settings.SamlEnabled || !identity.IsActive || identity.ProviderKey != settings.Saml.ProviderKey || identity.TenantId != settings.Saml.TenantId)
        {
            throw IdentityFault.AccessDisabled();
        }

        return StartSessionAsync(identity.UserId, "SAML", identity.ExternalIdentityId, previousId, correlationId, ct);
    }

    private async Task<LoginResult> StartSessionAsync( long userId, string method, long? externalIdentityId, Guid? previousId, string correlationId, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, ct) ?? throw IdentityFault.InvalidLogin();
        if (method == "LDAP" && user.DirectoryAccount is null) throw IdentityFault.InvalidLogin();
        var profile = AccessRules.Profile(user, await store.ActiveSitesAsync(ct));
        if (profile is null)
        {
            await store.AuditFailureAsync(user.UserId, "LOGIN_ACCESS_DENIED", correlationId, ct);
            throw IdentityFault.AccessDisabled();
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var session = new AuthSession
        {
            SessionId = Guid.NewGuid(), UserId = user.UserId, CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(settings.SessionMinutes), AuthenticationMethod = method, ExternalIdentityId = externalIdentityId
        };
        var secret = SessionSecrets.Create(session.SessionId);
        session.SecretHash = secret.Hash;
        await store.StartSessionAsync(session, previousId, correlationId, ct);
        return new(tokens.Issue(profile, session), secret.Cookie);
    }

    public async Task<AuthResponse> ResumeAsync(string? cookie, CancellationToken ct)
    {
        var session = await FindCookieSessionAsync(cookie, ct) ?? throw IdentityFault.SessionExpired();
        if (!CanUseSession(session)) throw IdentityFault.SessionExpired();
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
        if (session is null || session.UserId.ToString(CultureInfo.InvariantCulture) != subject
            || !CanUseSession(session))
        {
            return null;
        }
        return AccessRules.Profile(session.User, await store.ActiveSitesAsync(ct));
    }

    internal async Task<Guid?> ResolvePreviousSessionAsync(string? cookie, CancellationToken ct)
    {
        var session = await FindCookieSessionAsync(cookie, ct);
        return session?.SessionId;
    }

    private bool CanUseSession(AuthSession session)
    {
        if (session.RevokedAtUtc is not null || session.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || session.User is null || !session.User.IsActive)
        {
            return false;
        }

        if (session.AuthenticationMethod == "LDAP")
        {
            return settings.LdapEnabled && session.ExternalIdentityId is null && session.User.DirectoryAccount is not null;
        }

        if (session.AuthenticationMethod != "SAML" || !settings.SamlEnabled)
        {
            return false;
        }

        var identity = session.ExternalIdentity;

        return identity is not null &&
               identity.IsActive &&
               identity.ExternalIdentityId == session.ExternalIdentityId &&
               identity.UserId == session.UserId &&
               identity.ProviderKey == settings.Saml.ProviderKey &&
               identity.TenantId == settings.Saml.TenantId;
    }
    private async Task<AuthSession?> FindCookieSessionAsync(string? cookie, CancellationToken ct)
    {
        if (!SessionSecrets.TryRead(cookie, out var id, out var hash)) return null;
        var session = await store.FindSessionAsync(id, ct);
        return session is not null && SessionSecrets.Matches(session.SecretHash, hash) ? session : null;
    }
}
