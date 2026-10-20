using FinanceLogistics.Application.FinanceSources;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;

using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.FinanceSources;

public sealed partial class SqlFinanceImportService(PoDatabase database, LocalPoFileStorage files, FinanceSourceProfile definition) : IFinanceSourceImportService
{
    private bool Supports(string json) => FinanceSourcePolicy.Supports(definition, json);
    internal const string RequirementSql = """
        SELECT r.*,p.Status AS PeriodStatus,p.StartDate,p.EndDateExclusive
        FROM ingest.SubmissionRequirement r JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
        WHERE r.RequirementId=@id AND r.SourceCode=@source AND r.ScopeCode='FINANCE_SHARED';
        """;
    internal async Task<Dictionary<string, object?>> RequirementAsync(SqlConnection c, long id, CancellationToken ct)
    {
        await using var cmd = c.Command(RequirementSql, null, ("@id", id), ("@source", definition.SourceCode));
        return (await cmd.RowsAsync(ct)).SingleOrDefault()
            ?? throw new PoImportFault(404, "FINANCE_REQUIREMENT_NOT_FOUND", "Không có yêu cầu nộp nguồn Finance này.");
    }
    // All user mutations take locks in this order: period -> requirement -> existing batch.
    internal async Task<Dictionary<string, object?>> LockRequirementAsync(SqlConnection c, SqlTransaction tx,
        long id, int periodId, CancellationToken ct)
    {
        await using (var period = c.Command("SELECT PeriodId FROM cfg.ReportingPeriod WITH(UPDLOCK,HOLDLOCK) WHERE PeriodId=@period;",
            tx, ("@period", periodId)))
            await period.ExecuteScalarAsync(ct);
        await using var cmd = c.Command("""
            SELECT r.*,p.Status AS PeriodStatus,p.StartDate,p.EndDateExclusive
            FROM ingest.SubmissionRequirement r WITH(UPDLOCK,HOLDLOCK)
            JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
            WHERE r.RequirementId=@id AND r.PeriodId=@period
            AND r.SourceCode=@source AND r.ScopeCode='FINANCE_SHARED';
            """, tx, ("@id", id), ("@period", periodId), ("@source", definition.SourceCode));
        return (await cmd.RowsAsync(ct)).SingleOrDefault()
            ?? throw new PoImportFault(409, "REQUIREMENT_CHANGED", "Yêu cầu nộp đã thay đổi; tải lại trang.");
    }

    public async Task<IReadOnlyList<PoTemplateDto>> TemplatesAsync(CancellationToken ct)
    {
        await using var c = await database.OpenAsync(ct);
        await using var cmd = c.Command("""
            SELECT TemplateVersionId,VersionNo,DefinitionJson FROM cfg.TemplateVersion
            WHERE SourceCode=@source AND Status='APPROVED' ORDER BY VersionNo DESC;
            """, null, ("@source", definition.SourceCode));
        return (await cmd.RowsAsync(ct)).Where(x => Supports(x.Text("DefinitionJson")))
            .Select(x => new PoTemplateDto(PoProfile.Id(x.Long("TemplateVersionId")), x.Int("VersionNo"),
                PoProfile.ProfileCode(x.Text("DefinitionJson")), definition.Headers)).ToArray();
    }

    public async Task<UploadPoResult> UploadAsync(UploadPoCommand command, Stream content, CancellationToken ct)
    {
        if (command.RequirementId <= 0 || command.TemplateVersionId <= 0 || command.ClientUploadId == Guid.Empty)
            throw new PoImportFault(400, "INVALID_UPLOAD", "Thiếu yêu cầu nộp, phiên bản mẫu hoặc mã lần gửi.");
        var originalName = Path.GetFileName(command.OriginalName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 260 || originalName.Any(char.IsControl))
            throw new PoImportFault(400, "INVALID_FILE_NAME", "Tên file không hợp lệ hoặc dài hơn 260 ký tự.");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (extension != ".xlsx") throw new PoImportFault(400,"PROFILE_REQUIRES_XLSX","Bốn mẫu Finance này chỉ nhận XLSX nguyên bản đã kiểm tra.");
        var media="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        await using var c = await database.OpenAsync(ct);
        await FinanceSourceSql.RequireFinanceAsync(c, null, command.ActorId, definition.SourceCode, ct);
        var initial = await RequirementAsync(c, command.RequirementId, ct);
        var limits = PoProfile.Limits(initial.Text("PolicySnapshotJson"));
        var stored = await files.SaveAsync(content, originalName, limits.Bytes, ct);
        var commitAttempted = false;
        try
        {
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
            var req = await LockRequirementAsync(c, tx, command.RequirementId, initial.Int("PeriodId"), ct);
            await FinanceSourceSql.RequireFinanceAsync(c, tx, command.ActorId, definition.SourceCode, ct);
            await using var templateCmd = c.Command("""
                SELECT Status,DefinitionJson FROM cfg.TemplateVersion WITH(HOLDLOCK)
                WHERE TemplateVersionId=@template AND SourceCode=@source;
                """, tx, ("@template", command.TemplateVersionId), ("@source", definition.SourceCode));
            var template = (await templateCmd.RowsAsync(ct)).SingleOrDefault();
            if (template is null || !Supports(template.Text("DefinitionJson")))
                throw new PoImportFault(409, "TEMPLATE_UNSUPPORTED", "Mẫu chưa được bộ đọc Finance hiện tại hỗ trợ.");
            var profile = PoProfile.ProfileCode(template.Text("DefinitionJson"));
            var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(PoProfile.Serialize(new {
                hash = Convert.ToHexString(stored.Sha256), command.TemplateVersionId, command.ActorId,
                originalName, profile
            })));
            await using (var previous = c.Command("""
                SELECT BatchId,VersionNo,Status,UploadFingerprint FROM ingest.ImportBatch
                WHERE RequirementId=@id AND ClientUploadId=@client;
                """, tx, ("@id", command.RequirementId), ("@client", command.ClientUploadId)))
            {
                var match = (await previous.RowsAsync(ct)).SingleOrDefault();
                if (match is not null)
                {
                    if (match["UploadFingerprint"] is not byte[] old || !CryptographicOperations.FixedTimeEquals(old, fingerprint))
                        throw new PoImportFault(409, "UPLOAD_KEY_REUSED", "Mã lần gửi đã được dùng cho nội dung khác. Chọn lại file để tạo lần gửi mới.");
                    // An upload retry reports the existing batch; it never activates that batch.
                    await tx.RollbackAsync(ct);
                    files.DeleteUnusedCandidate(stored.StorageKey);
                    return new(PoProfile.Id(match.Long("BatchId")), match.Int("VersionNo"), match.Text("Status"), true);
                }
            }
            if (req.Text("PeriodStatus") != "OPEN")
                throw new PoImportFault(409, "PERIOD_NOT_OPEN", "Kỳ không còn mở để nộp file.");
            if (template.Text("Status") != "APPROVED")
                throw new PoImportFault(409, "TEMPLATE_NOT_APPROVED", "Mẫu chưa được duyệt hoặc đã ngừng dùng.");
            limits = PoProfile.Limits(req.Text("PolicySnapshotJson"));
            if (stored.SizeBytes > limits.Bytes) throw new PoImportFault(413, "FILE_TOO_LARGE", "Dung lượng vượt chính sách kỳ.");
            var policy = FinanceSourcePolicy.Resolve(req.Text("PolicySnapshotJson"), DateOnly.FromDateTime(req.Date("StartDate")), DateOnly.FromDateTime(req.Date("EndDateExclusive")));
            var options = PoProfile.Serialize(new { profile, sourceCode=definition.SourceCode, periodMode=policy.PeriodMode, maxRows=limits.Rows,
                startDate=policy.Start.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                endDateExclusive=policy.EndExclusive.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                allowCachedAmountFormulas=policy.AllowCachedAmountFormulas,
                templateDefinition=JsonSerializer.Deserialize<JsonElement>(template.Text("DefinitionJson")) });
            await using var insert = c.Command("""
                INSERT ingest.StoredFile(StorageKey,OriginalName,MediaType,SizeBytes,Sha256,Purpose,CreatedByUserId)
                VALUES(@storage,@name,@media,@size,@hash,'INPUT',@actor);
                DECLARE @file bigint=CONVERT(bigint,SCOPE_IDENTITY());
                DECLARE @version int=(SELECT ISNULL(MAX(VersionNo),0)+1 FROM ingest.ImportBatch WHERE RequirementId=@requirement);
                INSERT ingest.ImportBatch(RequirementId,SourceCode,TemplateVersionId,FileId,VersionNo,Kind,Status,
                    IngestionMode,ParsingOptionsJson,UploadedByUserId,ClientUploadId,UploadFingerprint)
                VALUES(@requirement,@source,@template,@file,@version,'DATA','RECEIVED','SNAPSHOT',@options,@actor,@client,@fingerprint);
                DECLARE @batch bigint=CONVERT(bigint,SCOPE_IDENTITY());
                INSERT ops.ProcessingJob(JobType,IdempotencyKey,PayloadJson,Status,AvailableAtUtc)
                VALUES('FINANCE_VALIDATE',CONCAT('finance-validate:',@batch),CONCAT('{"batchId":"',@batch,'"}'),'QUEUED',SYSUTCDATETIME());
                SELECT @batch AS BatchId,@version AS VersionNo;
                """, tx, ("@storage", stored.StorageKey), ("@name", originalName), ("@media", media),
                ("@size", stored.SizeBytes), ("@hash", stored.Sha256), ("@actor", command.ActorId),
                ("@requirement", command.RequirementId), ("@template", command.TemplateVersionId),
                ("@options", options), ("@client", command.ClientUploadId), ("@fingerprint", fingerprint), ("@source", definition.SourceCode));
            var added = (await insert.RowsAsync(ct)).Single();
            var id = added.Long("BatchId");
            await PoSql.AuditAsync(c, tx, command.ActorId, "FINANCE_UPLOADED", id, command.CorrelationId, null,
                new { requirementId = PoProfile.Id(command.RequirementId), versionNo = added.Int("VersionNo"),
                    fileHash = Convert.ToHexString(stored.Sha256), late = req["DueAtUtc"] is DateTime due && DateTime.UtcNow > due }, ct);
            commitAttempted = true;
            await tx.CommitAsync(ct);
            return new(PoProfile.Id(id), added.Int("VersionNo"), "RECEIVED", false);
        }
        catch
        {
            // SQL commit may have succeeded despite a lost response: never delete its possible source.
            // The guide includes reconciliation of unreferenced files after storage/SQL failures.
            if (!commitAttempted) files.DeleteUnusedCandidate(stored.StorageKey);
            throw;
        }
    }
}
