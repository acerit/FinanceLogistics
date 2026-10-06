using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace FinanceLogistics.Api.Security;

public sealed class TokenService(IdentitySettings settings, TimeProvider clock)
{
    public AuthResponse Issue(UserProfile user, AuthSession session)
    {
        var now = clock.GetUtcNow();
        var sessionExpiry = new DateTimeOffset(DateTime.SpecifyKind(session.ExpiresAtUtc, DateTimeKind.Utc));
        var expiry = now.AddMinutes(settings.AccessTokenMinutes);
        if (expiry > sessionExpiry) expiry = sessionExpiry;
        var claims = Claims(user, session.SessionId).Append(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime, expiry.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKeyBase64)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), "Bearer", expiry, sessionExpiry, user);
    }

    public static IEnumerable<Claim> Claims(UserProfile user, Guid sessionId)
    {
        yield return new("sub", user.UserId);
        yield return new("name", user.Username);
        yield return new("session_id", sessionId.ToString("D"));
        foreach (var role in user.Roles) yield return new("role", role);
        foreach (var site in user.Sites) yield return new("site", site);
        foreach (var permission in user.Permissions) yield return new("permission", permission);
    }
    public static ClaimsPrincipal Principal(UserProfile user, Guid sessionId) =>
        new(new ClaimsIdentity(Claims(user, sessionId), "Bearer", "name", "role"));
}
