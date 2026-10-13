using System.Data;
using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;

namespace FinanceLogistics.Infrastructure.Imports;

public sealed partial class SqlPoJobProcessor
{
    private async Task SaveResultAsync(PoJobLease lease, ParsedPoFile parsed, CancellationToken ct)
    {
        await using var c = await database.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        // Fence stale workers: both lease owner and attempt must still match while holding the job lock.
        await using (var guard = c.Command("""
            UPDATE ops.ProcessingJob WITH(UPDLOCK,HOLDLOCK) SET LeaseUntilUtc=DATEADD(minute,5,SYSUTCDATETIME())
            WHERE JobId=@job AND Status='RUNNING' AND LeaseOwner=@owner AND AttemptCount=@attempt
              AND LeaseUntilUtc>SYSUTCDATETIME();
            """, tx, ("@job", lease.JobId), ("@owner", lease.Owner), ("@attempt", lease.Attempt)))
            if (await guard.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Lease lost before commit.");
        await using (var batch = c.Command("SELECT Status FROM ingest.ImportBatch WITH(UPDLOCK,HOLDLOCK) WHERE BatchId=@batch;",
            tx, ("@batch", lease.BatchId)))
            if (Convert.ToString(await batch.ExecuteScalarAsync(ct)) != "VALIDATING")
                throw new InvalidOperationException("Batch no longer allows validation writes.");
        await using (var clear = c.Command("""
            DELETE src.PoUpdateLine WHERE BatchId=@batch;
            DELETE ingest.ImportIssue WHERE BatchId=@batch;
            DELETE ingest.RawRow WHERE BatchId=@batch;
            """, tx, ("@batch", lease.BatchId))) await clear.ExecuteNonQueryAsync(ct);

        using var raw = new DataTable();
        raw.Columns.Add("BatchId", typeof(long)); raw.Columns.Add("SheetName", typeof(string));
        raw.Columns.Add("RecordNumber", typeof(int)); raw.Columns.Add("CellsJson", typeof(string));
        foreach (var row in parsed.Rows) raw.Rows.Add(lease.BatchId, row.SheetName, row.RecordNumber, PoProfile.Serialize(row.Cells));
        await BulkAsync(c, tx, "ingest.RawRow", raw, ct);
        await using var readIds = c.Command("SELECT RawRowId,RecordNumber,SheetName FROM ingest.RawRow WHERE BatchId=@batch;", tx, ("@batch", lease.BatchId));
        var ids = (await readIds.RowsAsync(ct)).ToDictionary(r => (r.Text("SheetName"), r.Int("RecordNumber")), r => r.Long("RawRowId"));

        using var lines = new DataTable();
        lines.Columns.Add("BatchId", typeof(long)); lines.Columns.Add("RawRowId", typeof(long));
        lines.Columns.Add("SourceCode", typeof(string));
        var properties = PoProfile.Properties.Select(name => typeof(FinanceLogistics.Domain.Imports.PoUpdateLine).GetProperty(name)!).ToArray();
        foreach (var property in properties)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            lines.Columns.Add(property.Name, type == typeof(DateOnly) ? typeof(DateTime) : type);
        }
        foreach (var row in parsed.Rows.Where(x => x.Line is not null))
        {
            var values = new object[20]; values[0] = lease.BatchId; values[1] = ids[(row.SheetName, row.RecordNumber)]; values[2] = "PO_UPDATED";
            for (var i = 0; i < properties.Length; i++)
            {
                var value = properties[i].GetValue(row.Line);
                values[i + 3] = value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : value ?? DBNull.Value;
            }
            lines.Rows.Add(values);
        }
        await BulkAsync(c, tx, "src.PoUpdateLine", lines, ct);

        using var issues = new DataTable();
        issues.Columns.Add("BatchId", typeof(long)); issues.Columns.Add("RawRowId", typeof(long));
        issues.Columns.Add("Severity", typeof(string)); issues.Columns.Add("IssueCode", typeof(string));
        issues.Columns.Add("ColumnName", typeof(string)); issues.Columns.Add("Message", typeof(string));
        foreach (var issue in parsed.Issues)
        {
            object rowId = issue.RecordNumber is int number && issue.SheetName is string sheet
                && ids.TryGetValue((sheet, number), out var id) ? id : DBNull.Value;
            issues.Rows.Add(lease.BatchId, rowId, issue.Severity, issue.Code, (object?)issue.Column ?? DBNull.Value, issue.Message);
        }
        await BulkAsync(c, tx, "ingest.ImportIssue", issues, ct);
        var status = parsed.Errors == 0 && parsed.Rows.Count > 0 ? "READY" : "INVALID";
        await using var finish = c.Command("""
            UPDATE ingest.ImportBatch SET Status=@status,TotalRows=@rows,ErrorCount=@errors,WarningCount=@warnings WHERE BatchId=@batch;
            UPDATE ops.ProcessingJob SET Status='SUCCEEDED',CompletedAtUtc=SYSUTCDATETIME(),
                LeaseOwner=NULL,LeaseUntilUtc=NULL,LastErrorCode=NULL WHERE JobId=@job;
            """, tx, ("@status", status), ("@rows", parsed.Rows.Count), ("@errors", parsed.Errors),
            ("@warnings", parsed.Warnings), ("@batch", lease.BatchId), ("@job", lease.JobId));
        await finish.ExecuteNonQueryAsync(ct);
        await PoSql.AuditAsync(c, tx, null, "PO_VALIDATED", lease.BatchId, Guid.NewGuid(), null,
            new { status, totalRows = parsed.Rows.Count, errorCount = parsed.Errors, warningCount = parsed.Warnings, jobId = PoProfile.Id(lease.JobId) }, ct);
        await tx.CommitAsync(ct);
    }
    private static async Task BulkAsync(SqlConnection c, SqlTransaction tx, string table, DataTable data, CancellationToken ct)
    {
        if (data.Rows.Count == 0) return;
        using var bulk = new SqlBulkCopy(c, SqlBulkCopyOptions.CheckConstraints, tx)
        { DestinationTableName = table, BatchSize = 1000, BulkCopyTimeout = 90 };
        foreach (DataColumn column in data.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulk.WriteToServerAsync(data, ct);
    }
}
