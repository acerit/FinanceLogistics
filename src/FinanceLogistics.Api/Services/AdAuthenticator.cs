using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Models;

namespace FinanceLogistics.Api.Services;

public interface IAdAuthenticator
{
    bool Validate(AppUser user, string password);
}

public sealed class AdAuthenticator(IdentitySettings settings, ILogger<AdAuthenticator> logger) : IAdAuthenticator
{
    public bool Validate(AppUser user, string password)
    {
        var account = user.DirectoryAccount;
        if (account is null || !settings.Directories.TryGetValue(account.DirectoryKey, out var directory))
            throw new IdentityFault(503, "DIRECTORY_CONFIGURATION", "Cấu hình đăng nhập chưa hoàn tất. Liên hệ IT.");
        if (password.Length == 0) return false;
        try
        {
            const ContextOptions options = ContextOptions.Negotiate | ContextOptions.Signing | ContextOptions.Sealing;
            // Domain and SAM name come from trusted configuration/provisioning, never an arbitrary user-supplied suffix.
            using var context = new PrincipalContext(ContextType.Domain, directory.DomainName,
                string.IsNullOrWhiteSpace(directory.SearchBaseDn) ? null : directory.SearchBaseDn,
                options, $"{directory.NetBiosName}\\{account.SamAccountName}", password);
            if (!context.ValidateCredentials(account.SamAccountName, password, options)) return false;
            using var principal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, account.SamAccountName);
            if (principal?.Sid is null || principal.Enabled != true || principal.IsAccountLockedOut()) return false;
            if (principal.AccountExpirationDate is DateTime expiry && expiry.ToUniversalTime() <= DateTime.UtcNow) return false;
            return string.Equals(principal.Sid.Value, user.WindowsSid, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is PrincipalException or COMException or System.Security.Authentication.AuthenticationException)
        {
            // Intentionally do not log credentials, username, tokens, or the exception's server message.
            logger.LogWarning("Directory validation unavailable ({ExceptionType}).", ex.GetType().Name);
            throw new IdentityFault(503, "DIRECTORY_UNAVAILABLE", "Không thể xác thực với hệ thống tài khoản công ty. Vui lòng thử lại sau.");
        }
    }
}
