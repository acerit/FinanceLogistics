using System.ComponentModel.DataAnnotations;

namespace FinanceLogistics.Api.Contracts;

public sealed class LoginRequest
{
    [Required, StringLength(320)] public string Username { get; init; } = "";
    // Never trim or normalize a password. Empty input is rejected in AuthService.
    [Required(AllowEmptyStrings = true), StringLength(1024)]
    public string Password { get; init; } = "";
}

public sealed record UserProfile(
    string UserId, string Username, string? Upn, string DisplayName,
    string[] Roles, string[] Sites, string[] Permissions);

public sealed record AuthResponse(
    string Token, string TokenType, DateTimeOffset ExpiresAtUtc,
    DateTimeOffset SessionExpiresAtUtc, UserProfile User);

public sealed record ApiError(string Code, string Message, string CorrelationId);

public sealed record SourceAccess(
    string SourceCode, string ScopeCode, string OwnerRole, string[] Sites, bool IsActive);

public sealed record SourceAccessResponse(string SourceCode, string ScopeCode, string[] Sites);
