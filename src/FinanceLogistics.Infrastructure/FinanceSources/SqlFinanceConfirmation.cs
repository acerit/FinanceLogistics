using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Domain.Imports;

using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.FinanceSources;

public sealed partial class SqlFinanceImportService
{
    public async Task<ConfirmPoResult> ConfirmAtomicallyAsync(ConfirmPoCommand command, CancellationToken ct)
    {
        if(command.BatchId<=0||command.CurrentActorId<=0||command.ExpectedRequirementRowVersion.Length!=8)
            throw new PoImportFault(400,"INVALID_CONFIRM","Thiếu batch, người xác nhận hoặc rowversion.");
        await using var c = await database.OpenAsync(ct);
        var initial = await BatchAsync(c, command.BatchId, ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var req = await LockRequirementAsync(c, tx, initial.Long("RequirementId"), initial.Int("PeriodId"), ct);
        await FinanceSourceSql.RequireFinanceAsync(c, tx, command.CurrentActorId, definition.SourceCode, ct);
        await using var read = c.Command($"""
            SELECT b.*,t.Status AS TemplateStatus,t.DefinitionJson,
                (SELECT COUNT(*) FROM {definition.SqlTable} l WHERE l.BatchId=b.BatchId) AS TypedRows,
                (SELECT COUNT(*) FROM ingest.ImportIssue i WHERE i.BatchId=b.BatchId AND i.Severity='ERROR') AS PersistedErrors
            FROM ingest.ImportBatch b WITH(UPDLOCK,HOLDLOCK)
            JOIN cfg.TemplateVersion t WITH(HOLDLOCK) ON t.TemplateVersionId=b.TemplateVersionId
            WHERE b.BatchId=@id AND b.RequirementId=@requirement;
            """, tx, ("@id", command.BatchId), ("@requirement", initial.Long("RequirementId")));
        var batch = (await read.RowsAsync(ct)).Single();
        var current = req["ActiveBatchId"] is long active && active == command.BatchId;
        var evaluation = ConfirmPolicy.Evaluate(new ConfirmFacts(
            Enum.Parse<PeriodStatus>(req.Text("PeriodStatus"), ignoreCase: true),
            Enum.Parse<BatchStatus>(batch.Text("Status"), ignoreCase: true), current,
            req.Bytes("RowVersion").SequenceEqual(command.ExpectedRequirementRowVersion),
            batch.Int("ErrorCount"), batch.Int("WarningCount"), command.AcceptWarnings,
            batch.Text("IngestionMode") == "SNAPSHOT", batch.Int("TotalRows"),
            batch.Text("TemplateStatus") == "APPROVED" && Supports(batch.Text("DefinitionJson"))));
        if (evaluation.Decision == ConfirmDecision.Reject)
            throw new PoImportFault(409, evaluation.ErrorCode!, Message(evaluation.ErrorCode!));
        if (evaluation.Decision == ConfirmDecision.AlreadyCurrent)
        {
            await tx.CommitAsync(ct);
            return new(command.BatchId, req.Long("RequirementId"), true);
        }
        if (batch.Int("TypedRows") != batch.Int("TotalRows") || batch.Int("PersistedErrors") != 0)
            throw new PoImportFault(409, "VALIDATION_INCONSISTENT", "Kết quả kiểm tra không khớp các dòng đã lưu; cần kiểm tra job.");
        await using var update = c.Command("""
            UPDATE ingest.ImportBatch SET Status='COMMITTED',CommittedByUserId=@actor,CommittedAtUtc=SYSUTCDATETIME()
            WHERE BatchId=@batch AND Status='READY';
            UPDATE ingest.SubmissionRequirement SET ActiveBatchId=@batch WHERE RequirementId=@requirement;
            UPDATE cfg.ReportingPeriod SET DataRevision=DataRevision+1 WHERE PeriodId=@period;
            """, tx, ("@actor", command.CurrentActorId), ("@batch", command.BatchId),
            ("@requirement", req.Long("RequirementId")), ("@period", req.Int("PeriodId")));
        await update.ExecuteNonQueryAsync(ct);
        await PoSql.AuditAsync(c, tx, command.CurrentActorId, "FINANCE_CONFIRMED", command.BatchId, command.CorrelationId,
            new { activeBatchId = req.Maybe("ActiveBatchId") },
            new { activeBatchId = PoProfile.Id(command.BatchId), command.AcceptWarnings, warningCount = batch.Int("WarningCount") }, ct);
        await tx.CommitAsync(ct);
        return new(command.BatchId, req.Long("RequirementId"), false);
    }
    private static string Message(string code) => code switch
    {
        "BATCH_SUPERSEDED" => "Bản này đã được một bản khác thay thế; không thể dùng retry để kích hoạt lại bản cũ.",
        "PERIOD_NOT_OPEN" => "Kỳ đã chuyển sang REVIEW hoặc CLOSED.",
        "REQUIREMENT_CHANGED" => "Bản đang dùng đã thay đổi từ lúc bạn xem preview. Tải lại preview, kiểm tra rồi xác nhận lại.",
        "BATCH_NOT_READY" => "File chưa READY hoặc còn lỗi ERROR.",
        "WARNINGS_NOT_ACCEPTED" => "Bạn cần kiểm tra và chấp nhận các cảnh báo.",
        "TEMPLATE_NOT_APPROVED" => "Mẫu dùng cho file này không còn được duyệt.",
        "EMPTY_DATA_BATCH" => "File rỗng không được thay bản đang dùng.",
        _ => "Lần nộp chưa đủ điều kiện xác nhận: " + code
    };
}
