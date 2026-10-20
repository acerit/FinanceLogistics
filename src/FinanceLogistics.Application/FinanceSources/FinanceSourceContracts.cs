using FinanceLogistics.Application.Imports;
using FinanceLogistics.Application.Periods;
namespace FinanceLogistics.Application.FinanceSources;

public sealed record FinanceIssueGroupDto(string Severity,string IssueCode,int IssueCount);
public sealed record FinancePreviewRowDto(int RecordNumber,string SheetName,string RowKind,PoCellDto[] RawCells,string?[]? NormalizedCells);

public interface IFinanceImportFactory
{
    IFinanceSourceImportService For(string sourceCode);
}
// Technical DTOs already used by PO are shared; no dependency on concrete SQL/Excel implementations.
public interface IFinanceSourceImportService
{
    Task<IReadOnlyList<PoPeriodDto>> PeriodsAsync(CancellationToken ct);
    Task<IReadOnlyList<PoRequirementDto>> RequirementsAsync(int periodId,CancellationToken ct);
    Task<IReadOnlyList<PoTemplateDto>> TemplatesAsync(CancellationToken ct);
    Task<UploadPoResult> UploadAsync(UploadPoCommand command,Stream content,CancellationToken ct);
    Task<PoBatchDto> GetBatchAsync(long batchId,CancellationToken ct);
    Task<PoPage<PoHistoryDto>> HistoryAsync(long requirementId,int page,CancellationToken ct);
    Task<PoPage<FinancePreviewRowDto>> RowsAsync(long batchId,int page,CancellationToken ct);
    Task<PoPage<PoIssueDto>> IssuesAsync(long batchId,int page,CancellationToken ct,string? issueCode=null);
    Task<IReadOnlyList<FinanceIssueGroupDto>> IssueSummaryAsync(long batchId,CancellationToken ct);
    Task<PoOriginal> OpenOriginalAsync(long batchId,CancellationToken ct);
    Task<ConfirmPoResult> ConfirmAtomicallyAsync(ConfirmPoCommand command,CancellationToken ct);
    Task RetryValidationAsync(long batchId,long actorId,Guid correlationId,CancellationToken ct);
}
