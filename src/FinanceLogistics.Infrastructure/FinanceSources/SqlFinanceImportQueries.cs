using FinanceLogistics.Application.FinanceSources;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;

using FinanceLogistics.Infrastructure.Imports;
using FinanceLogistics.Application.Periods;
namespace FinanceLogistics.Infrastructure.FinanceSources;

public sealed partial class SqlFinanceImportService
{
    private const int PageSize = 50;
    private static int Offset(int page)
    {
        if (page is < 1 or > 100_000) throw new PoImportFault(400, "INVALID_PAGE", "Số trang không hợp lệ.");
        return (page - 1) * PageSize;
    }
    internal async Task<Dictionary<string, object?>> BatchAsync(SqlConnection c, long id, CancellationToken ct)
    {
        await using var cmd = c.Command($"""
            SELECT b.*,f.OriginalName,f.StorageKey,f.MediaType,f.Sha256,
                   r.ActiveBatchId,r.RowVersion AS RequirementRowVersion,r.PeriodId,
                   p.Status AS PeriodStatus,
                   (SELECT MIN([{definition.CoverageProperty}]) FROM {definition.SqlTable} WHERE BatchId=b.BatchId) AS CoverageStart,
                   (SELECT MAX([{definition.CoverageProperty}]) FROM {definition.SqlTable} WHERE BatchId=b.BatchId) AS CoverageEnd,
                   j.Status AS JobStatus,j.LastErrorCode AS JobErrorCode,j.AttemptCount AS JobAttempts
            FROM ingest.ImportBatch b JOIN ingest.StoredFile f ON f.FileId=b.FileId
            JOIN ingest.SubmissionRequirement r ON r.RequirementId=b.RequirementId
            JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
            LEFT JOIN ops.ProcessingJob j ON j.IdempotencyKey=CONCAT('finance-validate:',b.BatchId) AND j.JobType='FINANCE_VALIDATE'
            WHERE b.BatchId=@id AND b.SourceCode=@source AND r.ScopeCode='FINANCE_SHARED';
            """, null, ("@id", id), ("@source", definition.SourceCode));
        return (await cmd.RowsAsync(ct)).SingleOrDefault()
            ?? throw new PoImportFault(404, "FINANCE_BATCH_NOT_FOUND", "Không tìm thấy lần nộp Finance.");
    }
    public async Task<PoBatchDto> GetBatchAsync(long batchId, CancellationToken ct)
    {
        await using var c = await database.OpenAsync(ct);
        var b = await BatchAsync(c, batchId, ct);
        using var options = JsonDocument.Parse(b.Text("ParsingOptionsJson"));
        var settings = options.RootElement;
        var profile = settings.GetProperty("profile").GetString()!;
        var periodMode = settings.TryGetProperty("periodMode", out var mode) ? mode.GetString()! : PoProfile.SelectedMonth;
        return new(PoProfile.Id(batchId), PoProfile.Id(b.Long("RequirementId")), b.Int("VersionNo"), b.Text("Status"),
            b.Text("OriginalName"), b.Int("TotalRows"), b.Int("ErrorCount"), b.Int("WarningCount"), PoProfile.Utc(b.Date("UploadedAtUtc")),
            b["ActiveBatchId"] is long active && active == batchId, b.Maybe("ActiveBatchId"), Convert.ToBase64String(b.Bytes("RequirementRowVersion")),
            b.Text("PeriodStatus"), b.Maybe("JobStatus"), b.Maybe("JobErrorCode"), b.Int("JobAttempts"), definition.Headers,
            profile, periodMode,
            b["CoverageStart"] is DateTime first ? first.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            b["CoverageEnd"] is DateTime last ? last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null);
    }
    public async Task<PoPage<PoHistoryDto>> HistoryAsync(long requirementId, int page, CancellationToken ct)
    {
        var offset = Offset(page);
        await using var c = await database.OpenAsync(ct);
        var req = await RequirementAsync(c, requirementId, ct);
        await using var cmd = c.Command("""
            SELECT b.BatchId,b.VersionNo,b.Status,b.UploadedAtUtc,b.TotalRows,b.ErrorCount,b.WarningCount,f.OriginalName,
                   COUNT(*) OVER() AS Total
            FROM ingest.ImportBatch b JOIN ingest.StoredFile f ON f.FileId=b.FileId
            WHERE b.RequirementId=@id ORDER BY b.VersionNo DESC OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY;
            """, null, ("@id", requirementId), ("@offset", offset));
        var rows = await cmd.RowsAsync(ct);
        return new(page, PageSize, rows.FirstOrDefault()?.Int("Total") ?? 0, rows.Select(b => new PoHistoryDto(
            PoProfile.Id(b.Long("BatchId")), b.Int("VersionNo"), b.Text("Status"), b.Text("OriginalName"), PoProfile.Utc(b.Date("UploadedAtUtc")),
            req["ActiveBatchId"] is long active && active == b.Long("BatchId"), b.Int("TotalRows"), b.Int("ErrorCount"), b.Int("WarningCount"))).ToArray());
    }
    public async Task<PoPage<FinancePreviewRowDto>> RowsAsync(long batchId,int page,CancellationToken ct)
    {
        var offset=Offset(page);await using var c=await database.OpenAsync(ct);await BatchAsync(c,batchId,ct);
        var fields=string.Join(",",definition.Fields.Select(f=>f.Kind==FinanceFieldKind.Number
            ? $"CONVERT(varchar(80),p.[{f.Property}]) AS [{f.Property}]" : $"p.[{f.Property}]"));
        // Table/column identifiers come exclusively from the compiled, whitelisted profiles.
        await using var cmd=c.Command($"""
            SELECT rr.RecordNumber,rr.SheetName,rr.CellsJson,p.SourceLineId,{fields},COUNT(*) OVER() AS Total
            FROM ingest.RawRow rr LEFT JOIN {definition.SqlTable} p ON p.RawRowId=rr.RawRowId AND p.BatchId=rr.BatchId
            WHERE rr.BatchId=@id ORDER BY rr.RecordNumber OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY;
            """,null,("@id",batchId),("@offset",offset));
        var rows=await cmd.RowsAsync(ct);
        static string? Format(FinanceField f,object? value)=>value switch {
            null=>null, DateTime d=>d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
            string n when f.Kind==FinanceFieldKind.Number=>n.Contains('.')?n.TrimEnd('0').TrimEnd('.'):n,
            _=>Convert.ToString(value,CultureInfo.InvariantCulture) };
        return new(page,PageSize,rows.FirstOrDefault()?.Int("Total")??0,rows.Select(r=>{
            var raw=JsonSerializer.Deserialize<FinanceRawPayload>(r.Text("CellsJson"),PoProfile.Json)??throw new InvalidDataException("Missing raw payload.");
            return new FinancePreviewRowDto(r.Int("RecordNumber"),r.Text("SheetName"),raw.Kind,raw.Cells,
                r["SourceLineId"] is null?null:definition.Fields.Select(f=>Format(f,r[f.Property])).ToArray());
        }).ToArray());
    }
    public async Task<PoPage<PoIssueDto>> IssuesAsync(long batchId, int page, CancellationToken ct, string? issueCode = null)
    {
        if(string.IsNullOrWhiteSpace(issueCode))issueCode=null;
        if(issueCode?.Length>80)throw new PoImportFault(400,"INVALID_ISSUE_CODE","Mã nhóm lỗi quá dài.");
        var offset = Offset(page);
        await using var c = await database.OpenAsync(ct);
        await BatchAsync(c, batchId, ct);
        await using var cmd = c.Command("""
            SELECT i.Severity,i.IssueCode,i.ColumnName,i.Message,rr.RecordNumber,rr.SheetName,COUNT(*) OVER() AS Total
            FROM ingest.ImportIssue i LEFT JOIN ingest.RawRow rr ON rr.RawRowId=i.RawRowId AND rr.BatchId=i.BatchId
            WHERE i.BatchId=@id AND (@code IS NULL OR i.IssueCode=@code) ORDER BY i.IssueId OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY;
            """, null, ("@id", batchId), ("@offset", offset), ("@code", issueCode));
        var rows = await cmd.RowsAsync(ct);
        return new(page, PageSize, rows.FirstOrDefault()?.Int("Total") ?? 0, rows.Select(r => new PoIssueDto(
            r.Text("Severity"), r.Text("IssueCode"), r.Maybe("ColumnName"), r.Text("Message"),
            r["RecordNumber"] is null ? null : r.Int("RecordNumber"), r.Maybe("SheetName"))).ToArray());
    }
    public async Task<IReadOnlyList<FinanceIssueGroupDto>> IssueSummaryAsync(long batchId,CancellationToken ct)
    {
        await using var c=await database.OpenAsync(ct);await BatchAsync(c,batchId,ct);
        await using var cmd=c.Command("""
            SELECT Severity,IssueCode,COUNT(*) AS IssueCount FROM ingest.ImportIssue WHERE BatchId=@id
            GROUP BY Severity,IssueCode ORDER BY Severity,IssueCode;
            """,null,("@id",batchId));
        return (await cmd.RowsAsync(ct)).Select(r=>new FinanceIssueGroupDto(r.Text("Severity"),r.Text("IssueCode"),r.Int("IssueCount"))).ToArray();
    }
    public async Task<PoOriginal> OpenOriginalAsync(long batchId, CancellationToken ct)
    {
        await using var c = await database.OpenAsync(ct);
        var b = await BatchAsync(c, batchId, ct);
        try { return new(await files.OpenReadAsync(b.Text("StorageKey"), ct), b.Text("OriginalName"), b.Text("MediaType")); }
        catch (FileNotFoundException) { throw new PoImportFault(409, "SOURCE_FILE_MISSING", "File gốc không còn trong kho; cần kiểm tra backup/kho file."); }
    }
    public async Task RetryValidationAsync(long batchId, long actorId, Guid correlationId, CancellationToken ct)
    {
        await using var c = await database.OpenAsync(ct);
        await BatchAsync(c, batchId, ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        await FinanceSourceSql.RequireFinanceAsync(c, tx, actorId, definition.SourceCode, ct);
        // Job -> batch is the worker lock order. This action never changes the active pointer.
        await using var update = c.Command("""
            UPDATE ops.ProcessingJob SET Status='QUEUED',AttemptCount=0,AvailableAtUtc=SYSUTCDATETIME(),
                   LeaseOwner=NULL,LeaseUntilUtc=NULL,CompletedAtUtc=NULL,LastErrorCode=NULL
            WHERE JobType='FINANCE_VALIDATE' AND IdempotencyKey=CONCAT('finance-validate:',@id) AND Status='FAILED'
              AND EXISTS(SELECT 1 FROM ingest.ImportBatch b WHERE b.BatchId=@id AND b.Status IN('RECEIVED','VALIDATING'));
            """, tx, ("@id", batchId));
        if (await update.ExecuteNonQueryAsync(ct) != 1)
            throw new PoImportFault(409, "JOB_NOT_RETRYABLE", "Chỉ thử lại khi job lỗi kỹ thuật; dữ liệu INVALID cần nộp file đã sửa.");
        await PoSql.AuditAsync(c, tx, actorId, "FINANCE_VALIDATION_RETRIED", batchId, correlationId, null, new { status = "QUEUED" }, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<PoPeriodDto>> PeriodsAsync(CancellationToken ct)
    {
        await using var c=await database.OpenAsync(ct);
        await using var cmd=c.Command("""
            SELECT p.* FROM cfg.ReportingPeriod p WHERE EXISTS(SELECT 1 FROM ingest.SubmissionRequirement r
            WHERE r.PeriodId=p.PeriodId AND r.SourceCode=@source AND r.ScopeCode='FINANCE_SHARED') ORDER BY p.StartDate DESC;
            """,null,("@source",definition.SourceCode));
        return (await cmd.RowsAsync(ct)).Select(r=>new PoPeriodDto(PoProfile.Id(r.Int("PeriodId")),r.Text("PeriodCode"),
            DateOnly.FromDateTime(r.Date("StartDate")),DateOnly.FromDateTime(r.Date("EndDateExclusive")),r.Text("Status"))).ToArray();
    }
    public async Task<IReadOnlyList<PoRequirementDto>> RequirementsAsync(int periodId,CancellationToken ct)
    {
        await using var c=await database.OpenAsync(ct);
        await using var cmd=c.Command("""
            SELECT r.*,p.Status AS PeriodStatus,b.VersionNo AS ActiveVersionNo,b.Status AS ActiveBatchStatus
            FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
            LEFT JOIN ingest.ImportBatch b ON b.BatchId=r.ActiveBatchId
            WHERE r.PeriodId=@period AND r.SourceCode=@source AND r.ScopeCode='FINANCE_SHARED';
            """,null,("@period",periodId),("@source",definition.SourceCode));
        return (await cmd.RowsAsync(ct)).Select(r=>new PoRequirementDto(PoProfile.Id(r.Long("RequirementId")),PoProfile.Id(periodId),
            r.Text("SourceCode"),r.Text("ScopeCode"),r["DueAtUtc"] is DateTime due?new DateTimeOffset(DateTime.SpecifyKind(due,DateTimeKind.Utc)):null,
            r.Text("TimeZoneId"),(bool)r["IsRequired"]!,r.Maybe("ResponsibleUserId"),r.Maybe("ActiveBatchId"),
            r["ActiveVersionNo"] is int v?v:null,r.Maybe("ActiveBatchStatus"),r.Text("PeriodStatus"),Convert.ToBase64String(r.Bytes("RowVersion")))).ToArray();
    }
}
