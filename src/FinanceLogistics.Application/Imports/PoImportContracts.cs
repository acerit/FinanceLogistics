namespace FinanceLogistics.Application.Imports;

// IDs are strings in JSON: JavaScript must not round SQL bigint identifiers.
public sealed record PoTemplateDto(string TemplateVersionId, int VersionNo, string Profile, string[] Headers);
public sealed record UploadPoCommand(long RequirementId, long TemplateVersionId,
    Guid ClientUploadId, long ActorId, Guid CorrelationId, string OriginalName);
public sealed record UploadPoResult(string BatchId, int VersionNo, string Status, bool AlreadyReceived);
public sealed record PoBatchDto(string BatchId, string RequirementId, int VersionNo, string Status,
    string OriginalName, int TotalRows, int ErrorCount, int WarningCount, string UploadedAtUtc,
    bool IsCurrent, string? ActiveBatchId, string RequirementRowVersion, string PeriodStatus,
    string? JobStatus, string? JobErrorCode, int JobAttempts, string[] Headers, string Profile, string PeriodMode, string? CoverageStart, string? CoverageEnd);
public sealed record PoHistoryDto(string BatchId, int VersionNo, string Status, string OriginalName,
    string UploadedAtUtc, bool IsCurrent, int TotalRows, int ErrorCount, int WarningCount);
public sealed record PoCellDto(int Ordinal, string Header, string? Text, string CellType,
    string? CellReference = null, string? Formula = null, string? CachedValue = null,
    string? FormulaOrigin = null, string? FormulaKind = null, string? SharedFormulaId = null);
public sealed record PoPreviewRowDto(int RecordNumber, string SheetName,
    IReadOnlyList<PoCellDto> RawCells, IReadOnlyList<string?>? NormalizedCells);
public sealed record PoIssueDto(string Severity, string IssueCode, string? ColumnName,
    string Message, int? RecordNumber, string? SheetName);
public sealed record PoPage<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);
public sealed record PoOriginal(Stream Content, string OriginalName, string MediaType);

public interface IPoImportService
{
    Task<IReadOnlyList<PoTemplateDto>> TemplatesAsync(CancellationToken ct);
    Task<UploadPoResult> UploadAsync(UploadPoCommand command, Stream content, CancellationToken ct);
    Task<PoBatchDto> GetBatchAsync(long batchId, CancellationToken ct);
    Task<PoPage<PoHistoryDto>> HistoryAsync(long requirementId, int page, CancellationToken ct);
    Task<PoPage<PoPreviewRowDto>> RowsAsync(long batchId, int page, CancellationToken ct);
    Task<PoPage<PoIssueDto>> IssuesAsync(long batchId, int page, CancellationToken ct);
    Task<PoOriginal> OpenOriginalAsync(long batchId, CancellationToken ct);
    Task RetryValidationAsync(long batchId, long actorId, Guid correlationId, CancellationToken ct);
}

public sealed class PoImportFault(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
