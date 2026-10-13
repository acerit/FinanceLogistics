namespace FinanceLogistics.Application.Periods;

// Read contracts for PO_UPDATED / FINANCE_SHARED only at this milestone.
// IDs are strings at the JSON boundary; no business amounts are calculated here.
public sealed record PoPeriodDto(
    string PeriodId, string PeriodCode, DateOnly StartDate,
    DateOnly EndDateExclusive, string Status);

public sealed record PoRequirementDto(
    string RequirementId, string PeriodId, string SourceCode, string ScopeCode,
    DateTimeOffset? DueAtUtc, string TimeZoneId, bool IsRequired,
    string? ResponsibleUserId, string? ActiveBatchId,
    int? ActiveVersionNo, string? ActiveBatchStatus,
    string PeriodStatus, string RowVersion);

public interface IPoPeriodReader
{
    Task<IReadOnlyList<PoPeriodDto>> ListPeriodsAsync(CancellationToken ct);
    Task<IReadOnlyList<PoRequirementDto>> ListRequirementsAsync(int periodId, CancellationToken ct);
}
