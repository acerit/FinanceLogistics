namespace FinanceLogistics.Api.Models;

public sealed class AppUser
{
    public long UserId { get; set; }
    public string WindowsSid { get; set; } = "";
    public string? Upn { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DirectoryAccount? DirectoryAccount { get; set; }
    public List<UserRole> Roles { get; set; } = [];
    public List<UserSite> Sites { get; set; } = [];
}

public sealed class AppRole
{
    public string RoleCode { get; set; } = "";
    public string RoleName { get; set; } = "";
}

public sealed class UserRole
{
    public long UserId { get; set; }
    public string RoleCode { get; set; } = "";
    public long GrantedByUserId { get; set; }
    public DateTime GrantedAtUtc { get; set; }
}

public sealed class Site
{
    public string SiteCode { get; set; } = "";
    public string SiteName { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class UserSite
{
    public long UserId { get; set; }
    public string SiteCode { get; set; } = "";
}

public sealed class DirectoryAccount
{
    public long UserId { get; set; }
    public string DirectoryKey { get; set; } = "";
    public string SamAccountName { get; set; } = "";
}

public sealed class LoginAlias
{
    public string NormalizedAlias { get; set; } = "";
    public long UserId { get; set; }
}

public sealed class AuthSession
{
    public Guid SessionId { get; set; }
    public long UserId { get; set; }
    public byte[] SecretHash { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public AppUser User { get; set; } = null!;
    public string AuthenticationMethod { get; set; } = "LDAP";
    public long? ExternalIdentityId { get; set; }
    public ExternalIdentity? ExternalIdentity { get; set; }
}

public sealed class IdentityAudit
{
    public long EventId { get; set; }
    public long? UserId { get; set; }
    public long? ActorUserId { get; set; }
    public string EventCode { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = "";
}

public sealed class DataScope
{
    public string ScopeCode { get; set; } = "";
    public string ScopeName { get; set; } = "";
}
public sealed class ScopeSite
{
    public string ScopeCode { get; set; } = "";
    public string SiteCode { get; set; } = "";
}
public sealed class SourceType
{
    public string SourceCode { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string OwnerRoleCode { get; set; } = "";
    public bool IsActive { get; set; }
}
public sealed class SourceScope
{
    public string SourceCode { get; set; } = "";
    public string ScopeCode { get; set; } = "";
}
public sealed class ExternalIdentity
{
    public long ExternalIdentityId { get; set; }

    public string ProviderKey { get; set; } = "";
    public Guid TenantId { get; set; }
    public Guid ObjectId { get; set; }

    public long UserId { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public long CreatedByUserId { get; set; }
}

public sealed class SamlLoginRequest
{
    public string RequestId { get; set; } = "";
    public byte[] BrowserSecretHash { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    public Guid? PreviousSessionId { get; set; }
}

public sealed class SamlReplayProof
{
    public byte[] ProofHash { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
}