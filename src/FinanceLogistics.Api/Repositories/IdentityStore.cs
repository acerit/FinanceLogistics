using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Data;
using FinanceLogistics.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceLogistics.Api.Repositories;

public sealed class IdentityStore(IdentityDbContext db, TimeProvider clock) : IIdentityStore
{
    private IQueryable<AppUser> Users() => db.Users.AsNoTracking().Include(x => x.DirectoryAccount)
        .Include(x => x.Roles).Include(x => x.Sites).AsSplitQuery();

    public async Task<AppUser?> FindUserByAliasAsync(string alias, CancellationToken ct)
    {
        var id = await db.LoginAliases.Where(x => x.NormalizedAlias == alias)
            .Select(x => (long?)x.UserId).SingleOrDefaultAsync(ct);
        return id is null ? null : await FindUserAsync(id.Value, ct);
    }
    public Task<AppUser?> FindUserAsync(long id, CancellationToken ct) => Users().SingleOrDefaultAsync(x => x.UserId == id, ct);
    public Task<string[]> ActiveSitesAsync(CancellationToken ct) => db.Sites.AsNoTracking()
        .Where(x => x.IsActive).OrderBy(x => x.SiteCode).Select(x => x.SiteCode).ToArrayAsync(ct);

    public Task<AuthSession?> FindSessionAsync(Guid id, CancellationToken ct) => db.Sessions.AsNoTracking()
        .Include(x => x.User).ThenInclude(x => x.DirectoryAccount)
        .Include(x => x.User).ThenInclude(x => x.Roles)
        .Include(x => x.User).ThenInclude(x => x.Sites)
        .AsSplitQuery().SingleOrDefaultAsync(x => x.SessionId == id, ct);

    public async Task StartSessionAsync(AuthSession session, Guid? previousId, string correlationId, CancellationToken ct)
    {
        if (previousId is not null)
        {
            var previous = await db.Sessions.SingleOrDefaultAsync(x => x.SessionId == previousId, ct);
            if (previous is not null) previous.RevokedAtUtc = clock.GetUtcNow().UtcDateTime;
        }
        db.Sessions.Add(session);
        AddAudit(session.UserId, session.UserId, "LOGIN_SUCCESS", correlationId);
        // One SaveChanges transaction: new session, old-session revocation and audit.
        await db.SaveChangesAsync(ct);
    }
    public async Task RevokeSessionAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.SessionId == id, ct);
        if (session is null || session.RevokedAtUtc is not null) return;
        session.RevokedAtUtc = clock.GetUtcNow().UtcDateTime;
        AddAudit(session.UserId, session.UserId, "LOGOUT", correlationId);
        await db.SaveChangesAsync(ct);
    }
    public async Task AuditFailureAsync(long? userId, string eventCode, string correlationId, CancellationToken ct)
    {
        AddAudit(userId, null, eventCode, correlationId);
        await db.SaveChangesAsync(ct);
    }
    private void AddAudit(long? userId, long? actor, string code, string correlationId) => db.Audit.Add(new()
    {
        UserId = userId, ActorUserId = actor, EventCode = code,
        CorrelationId = correlationId, OccurredAtUtc = clock.GetUtcNow().UtcDateTime
    });

    public async Task<SourceAccess[]> SourcesAsync(CancellationToken ct)
    {
        var sources = await (from ss in db.SourceScopes.AsNoTracking()
            join source in db.Sources on ss.SourceCode equals source.SourceCode
            select new { ss.SourceCode, ss.ScopeCode, source.OwnerRoleCode, source.IsActive }).ToArrayAsync(ct);
        var scopeSites = await (from scope in db.ScopeSites.AsNoTracking()
            join site in db.Sites on scope.SiteCode equals site.SiteCode
            select new { scope.ScopeCode, site.SiteCode, site.IsActive }).ToArrayAsync(ct);
        return sources.Select(s => {
            var sites = scopeSites.Where(x => x.ScopeCode == s.ScopeCode).ToArray();
            return new SourceAccess(s.SourceCode, s.ScopeCode, s.OwnerRoleCode,
                sites.Select(x => x.SiteCode).Distinct().Order().ToArray(),
                s.IsActive && sites.Length > 0 && sites.All(x => x.IsActive));
        }).ToArray();
    }
}
