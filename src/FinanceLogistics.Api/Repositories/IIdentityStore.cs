using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Models;

namespace FinanceLogistics.Api.Repositories;

public interface IIdentityStore
{
    Task<AppUser?> FindUserByAliasAsync(string alias, CancellationToken ct);
    Task<AppUser?> FindUserAsync(long id, CancellationToken ct);
    Task<string[]> ActiveSitesAsync(CancellationToken ct);
    Task<AuthSession?> FindSessionAsync(Guid id, CancellationToken ct);
    Task StartSessionAsync(AuthSession session, Guid? previousId, string correlationId, CancellationToken ct);
    Task RevokeSessionAsync(Guid id, string correlationId, CancellationToken ct);
    Task AuditFailureAsync(long? userId, string eventCode, string correlationId, CancellationToken ct);
    Task<SourceAccess[]> SourcesAsync(CancellationToken ct);
}
