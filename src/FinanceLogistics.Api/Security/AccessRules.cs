using System.Globalization;
using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Models;

namespace FinanceLogistics.Api.Security;

public static class AccessRules
{
    public const string Finance = "FINANCE";
    public const string Logistics = "LOGISTICS";
    public const string SystemAdmin = "SYSTEM_ADMIN";
    private static readonly string[] KnownRoles = [Finance, Logistics, SystemAdmin];

    public static string NormalizeAlias(string username) => username.Trim().ToUpperInvariant();

    public static UserProfile? Profile(AppUser user, string[] activeSites)
    {
        if (!user.IsActive || user.DirectoryAccount is null) return null;
        var roles = user.Roles.Select(x => x.RoleCode).Where(KnownRoles.Contains).Distinct().Order().ToArray();
        if (roles.Length == 0) return null;
        var sites = roles.Contains(Finance) ? activeSites : user.Sites.Select(x => x.SiteCode)
            .Where(activeSites.Contains).Distinct().Order().ToArray();
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        if (roles.Contains(Finance))
            permissions.UnionWith(["sources.read", "sources.upload", "sources.confirm", "reports.read",
                "reports.run", "periods.manage", "business_settings.manage"]);
        if (roles.Contains(Logistics))
            permissions.UnionWith(["sources.read", "sources.upload", "sources.confirm"]);
        if (roles.Contains(SystemAdmin))
            permissions.UnionWith(["access.manage", "technical_settings.manage"]);
        return new(user.UserId.ToString(CultureInfo.InvariantCulture), user.DirectoryAccount.SamAccountName,
            user.Upn, user.DisplayName, roles, sites.Order().ToArray(), permissions.Order().ToArray());
    }

    // Source and scope MUST be loaded from the database, not accepted as ownership claims from the UI.
    public static bool CanAccessSource(UserProfile user, SourceAccess source, string operation = "read")
    {
        if (operation is not ("read" or "upload" or "confirm") || !source.IsActive || source.Sites.Length == 0)
            return false;
        if (!user.Permissions.Contains($"sources.{operation}")) return false;
        if (user.Roles.Contains(Finance)) return true;
        return user.Roles.Contains(Logistics) && source.OwnerRole == Logistics
            && source.Sites.All(s => user.Sites.Contains(s));
    }
}
