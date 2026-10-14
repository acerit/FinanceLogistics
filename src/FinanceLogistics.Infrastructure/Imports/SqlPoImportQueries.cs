using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;

namespace FinanceLogistics.Infrastructure.Imports;

public sealed partial class SqlPoImportService
{
    private const int PageSize = 50;
    private static int Offset(int page)
    {
        if (page is < 1 or > 100_000) throw new PoImportFault(400, "INVALID_PAGE", "Số trang không hợp lệ.");
        return (page - 1) * PageSize;
    }
    internal static async Task<Dictionary<string, object?>> BatchAsync(SqlConnection c, long id, CancellationToken ct)
    {
        await using var cmd = c.Command("""
            SELECT b.*,f.OriginalName,f.StorageKey,f.MediaType,f.Sha256,
                   r.ActiveBatchId,r.RowVersion AS RequirementRowVersion,r.PeriodId,
                   p.Status AS PeriodStatus,
                   (SELECT MIN(TransactionDate) FROM src.PoUpdateLine WHERE BatchId=b.BatchId) AS CoverageStart,
                   (SELECT MAX(TransactionDate) FROM src.PoUpdateLine WHERE BatchId=b.BatchId) AS CoverageEnd,
                   j.Status AS JobStatus,j.LastErrorCode AS JobErrorCode,j.AttemptCount AS JobAttempts
            FROM ingest.ImportBatch b JOIN ingest.StoredFile f ON f.FileId=b.FileId
            JOIN ingest.SubmissionRequirement r ON r.RequirementId=b.RequirementId
            JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
            LEFT JOIN ops.ProcessingJob j ON j.IdempotencyKey=CONCAT('po-validate:',b.BatchId) AND j.JobType='PO_VALIDATE'
            WHERE b.BatchId=@id AND b.SourceCode='PO_UPDATED' AND r.ScopeCode='FINANCE_SHARED';
            """, null, ("@id", id));
        return (await cmd.RowsAsync(ct)).SingleOrDefault()
            ?? throw new PoImportFault(404, "PO_BATCH_NOT_FOUND", "Không tìm thấy lần nộp PO.");
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
            b.Text("PeriodStatus"), b.Maybe("JobStatus"), b.Maybe("JobErrorCode"), b.Int("JobAttempts"), PoProfile.Headers,
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
    public async Task<PoPage<PoPreviewRowDto>> RowsAsync(long batchId, int page, CancellationToken ct)
    {
        var offset = Offset(page);
        await using var c = await database.OpenAsync(ct);
        await BatchAsync(c, batchId, ct); // Constrain every read to PO/FINANCE_SHARED, including guessed IDs.
        await using var cmd = c.Command("""
            SELECT rr.RecordNumber,rr.SheetName,rr.CellsJson,p.SourceLineId,p.SourceCodeText,p.ReceiptNumber,p.TransactionDate,p.PoNumber,
                   p.ReceiptQuantity,CONVERT(varchar(60),p.PoUnitPrice) AS PoUnitPrice,p.PoLineReference,
                   p.PoCurrency,p.PoRateDate,p.PoRate,p.ReceiptRate,p.ReceiptRateDate,
                   CONVERT(varchar(60),p.PurchaseFunctionalAmount) AS PurchaseFunctionalAmount,p.InvoiceNumber,
                   p.InvoiceFunctionalAmount,CONVERT(varchar(60),p.InvoiceAmount) AS InvoiceAmount,
                   p.LineDescription,COUNT(*) OVER() AS Total
            FROM ingest.RawRow rr LEFT JOIN src.PoUpdateLine p ON p.RawRowId=rr.RawRowId AND p.BatchId=rr.BatchId
            WHERE rr.BatchId=@id ORDER BY rr.RecordNumber OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY;
            """, null, ("@id", batchId), ("@offset", offset));
        var rows = await cmd.RowsAsync(ct);
        static string? Format(object? value) => value switch {
            null => null, DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture), _ => value.ToString()
        };
        static string? FormatField(string name, object? value) =>
            (name is "PoUnitPrice" or "PurchaseFunctionalAmount" or "InvoiceAmount") && value is string number
                ? number.Contains('.') ? number.TrimEnd('0').TrimEnd('.') : number
                : Format(value);
        return new(page, PageSize, rows.FirstOrDefault()?.Int("Total") ?? 0, rows.Select(r => new PoPreviewRowDto(
            r.Int("RecordNumber"), r.Text("SheetName"), JsonSerializer.Deserialize<PoCellDto[]>(r.Text("CellsJson"), PoProfile.Json) ?? [],
            r["SourceLineId"] is null ? null : PoProfile.Properties.Select(name => FormatField(name, r[name])).ToArray())).ToArray());
    }
    public async Task<PoPage<PoIssueDto>> IssuesAsync(long batchId, int page, CancellationToken ct)
    {
        var offset = Offset(page);
        await using var c = await database.OpenAsync(ct);
        await BatchAsync(c, batchId, ct);
        await using var cmd = c.Command("""
            SELECT i.Severity,i.IssueCode,i.ColumnName,i.Message,rr.RecordNumber,rr.SheetName,COUNT(*) OVER() AS Total
            FROM ingest.ImportIssue i LEFT JOIN ingest.RawRow rr ON rr.RawRowId=i.RawRowId AND rr.BatchId=i.BatchId
            WHERE i.BatchId=@id ORDER BY i.IssueId OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY;
            """, null, ("@id", batchId), ("@offset", offset));
        var rows = await cmd.RowsAsync(ct);
        return new(page, PageSize, rows.FirstOrDefault()?.Int("Total") ?? 0, rows.Select(r => new PoIssueDto(
            r.Text("Severity"), r.Text("IssueCode"), r.Maybe("ColumnName"), r.Text("Message"),
            r["RecordNumber"] is null ? null : r.Int("RecordNumber"), r.Maybe("SheetName"))).ToArray());
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
        await PoSql.RequireFinanceAsync(c, tx, actorId, ct);
        // Job -> batch is the worker lock order. This action never changes the active pointer.
        await using var update = c.Command("""
            UPDATE ops.ProcessingJob SET Status='QUEUED',AttemptCount=0,AvailableAtUtc=SYSUTCDATETIME(),
                   LeaseOwner=NULL,LeaseUntilUtc=NULL,CompletedAtUtc=NULL,LastErrorCode=NULL
            WHERE JobType='PO_VALIDATE' AND IdempotencyKey=CONCAT('po-validate:',@id) AND Status='FAILED'
              AND EXISTS(SELECT 1 FROM ingest.ImportBatch b WHERE b.BatchId=@id AND b.Status IN('RECEIVED','VALIDATING'));
            """, tx, ("@id", batchId));
        if (await update.ExecuteNonQueryAsync(ct) != 1)
            throw new PoImportFault(409, "JOB_NOT_RETRYABLE", "Chỉ thử lại khi job lỗi kỹ thuật; dữ liệu INVALID cần nộp file đã sửa.");
        await PoSql.AuditAsync(c, tx, actorId, "PO_VALIDATION_RETRIED", batchId, correlationId, null, new { status = "QUEUED" }, ct);
        await tx.CommitAsync(ct);
    }
}
