using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Models;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using System.Security.Cryptography;

// Executable checks with fake storage/directory. These do not certify AD connectivity or SQL schema execution.
var passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAILED: " + description);
    Console.WriteLine("PASS: " + description); passed++;
}
var store = new FakeStore();
var user = store.User;
var logistics1000 = AccessRules.Profile(user, ["1000", "6000"])!;
var source1000 = new SourceAccess("LOGISTICS_1000", "SITE_1000", "LOGISTICS", ["1000"], true);
var source6000 = new SourceAccess("LOGISTICS_6000", "SITE_6000", "LOGISTICS", ["6000"], true);
var po = new SourceAccess("PO_UPDATED", "FINANCE_SHARED", "FINANCE", ["1000", "6000"], true);
Check(AccessRules.CanAccessSource(logistics1000, source1000, "upload"), "Logistics 1000 can upload its source");
Check(!AccessRules.CanAccessSource(logistics1000, source6000), "Logistics 1000 cannot read site 6000");
Check(!AccessRules.CanAccessSource(logistics1000, po), "Logistics cannot access PO even when one site matches");
Check(!AccessRules.CanAccessSource(logistics1000, source1000 with { Sites = ["1000", "6000"] }), "Multi-site scope needs every site");
Check(!AccessRules.CanAccessSource(logistics1000, source1000 with { Sites = [] }), "An empty scope does not grant access");
Check(!AccessRules.CanAccessSource(logistics1000, source1000 with { IsActive = false }), "Disabled source is rejected");
Check(!AccessRules.CanAccessSource(logistics1000, source1000, "delete"), "Unknown action is denied");
user.Roles = [new() { RoleCode = "SYSTEM_ADMIN" }];
Check(!AccessRules.CanAccessSource(AccessRules.Profile(user, ["1000", "6000"])!, po), "System admin is not automatically Finance");
user.Roles = [new() { RoleCode = "FINANCE" }, new() { RoleCode = "SYSTEM_ADMIN" }];
var finance = AccessRules.Profile(user, ["1000", "6000"])!;
Check(finance.Roles.Length == 2 && finance.Sites.Length == 2 && AccessRules.CanAccessSource(finance, po), "Multiple roles retained; Finance covers active sites");
user.IsActive = false;
Check(AccessRules.Profile(user, ["1000"]) is null, "Disabled user cannot obtain an active profile");
user.IsActive = true; user.Roles = [new() { RoleCode = "LOGISTICS" }];

var directory = new FakeDirectory();
var settings = new IdentitySettings {
    Issuer = "CheckIssuer", Audience = "CheckAudience",
    SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    AccessTokenMinutes = 10, SessionMinutes = 120
};
var service = new AuthService(store, directory, new TokenService(settings, TimeProvider.System), settings, TimeProvider.System);
var login = await service.LoginAsync(new() { Username = " SAMPLE ", Password = " exact password " }, null, "check", default);
Check(directory.LastPassword == " exact password ", "Password reaches AD adapter without trimming");
Check(store.LastAlias == "SAMPLE", "Username alias is normalized without deleting domain suffixes");
Check((await service.ResumeAsync(login.SessionCookie, default)).User.UserId == "7", "Session cookie restores the same app user");
Check(SessionSecrets.TryRead(login.SessionCookie, out var sessionId, out var hash) && hash.Length == 32, "Session secret is represented by a 32-byte hash");
Check(!SessionSecrets.Matches(hash, RandomNumberGenerator.GetBytes(32)), "Wrong session secret is rejected");
Check(await service.ValidateAccessAsync(sessionId, "999", default) is null, "JWT subject must belong to its server session");
user.Sites = [new() { UserId = 7, SiteCode = "6000" }];
var changed = await service.ValidateAccessAsync(sessionId, "7", default);
Check(changed!.Sites.SequenceEqual(["6000"]), "Existing JWT uses the current database site assignment");
user.IsActive = false;
Check(await service.ValidateAccessAsync(sessionId, "7", default) is null, "Disabling account blocks an already-issued token on next request");
user.IsActive = true;
await service.LogoutAsync(login.SessionCookie, "check", default);
Check(await service.ValidateAccessAsync(sessionId, "7", default) is null, "Logout revokes the server session, including already-issued tokens");
try { await service.ResumeAsync(login.SessionCookie, default); Check(false, "Revoked session must fail"); }
catch (IdentityFault e) { Check(e.Status == 401, "Revoked cookie cannot restore a session"); }
directory.Valid = false;
try { await service.LoginAsync(new() { Username = "SAMPLE", Password = "wrong" }, null, "check", default); Check(false, "Wrong credentials must fail"); }
catch (IdentityFault e) { Check(e.Code == "INVALID_CREDENTIALS", "Invalid AD credentials are rejected"); }
Check(store.Sessions.Count == 1, "Failed login does not create a session");
Console.WriteLine($"Completed {passed} checks.");

sealed class FakeDirectory : IAdAuthenticator
{
    public string? LastPassword { get; private set; }
    public bool Valid { get; set; } = true;
    public bool Validate(AppUser user, string password) { LastPassword = password; return Valid; }
}
sealed class FakeStore : IIdentityStore
{
    public AppUser User { get; } = new() {
        UserId = 7, WindowsSid = "S-1-5-21-1-2-3-1001", DisplayName = "Synthetic Test User", IsActive = true,
        DirectoryAccount = new() { UserId = 7, DirectoryKey = "TEST", SamAccountName = "sample" },
        Roles = [new() { UserId = 7, RoleCode = "LOGISTICS" }], Sites = [new() { UserId = 7, SiteCode = "1000" }]
    };
    public Dictionary<Guid, AuthSession> Sessions { get; } = new();
    public string? LastAlias { get; private set; }
    public Task<AppUser?> FindUserByAliasAsync(string alias, CancellationToken ct) { LastAlias = alias; return Task.FromResult<AppUser?>(alias == "SAMPLE" ? User : null); }
    public Task<AppUser?> FindUserAsync(long id, CancellationToken ct) => Task.FromResult<AppUser?>(id == User.UserId ? User : null);
    public Task<string[]> ActiveSitesAsync(CancellationToken ct) => Task.FromResult<string[]>(["1000", "6000"]);
    public Task<AuthSession?> FindSessionAsync(Guid id, CancellationToken ct) => Task.FromResult(Sessions.GetValueOrDefault(id));
    public Task StartSessionAsync(AuthSession session, Guid? previousId, string correlationId, CancellationToken ct) {
        if (previousId is Guid previous && Sessions.TryGetValue(previous, out var old)) old.RevokedAtUtc = DateTime.UtcNow;
        session.User = User; Sessions.Add(session.SessionId, session); return Task.CompletedTask;
    }
    public Task RevokeSessionAsync(Guid id, string correlationId, CancellationToken ct) {
        if (Sessions.TryGetValue(id, out var session)) session.RevokedAtUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }
    public Task AuditFailureAsync(long? userId, string eventCode, string correlationId, CancellationToken ct) => Task.CompletedTask;
    public Task<SourceAccess[]> SourcesAsync(CancellationToken ct) => Task.FromResult<SourceAccess[]>([]);
}
