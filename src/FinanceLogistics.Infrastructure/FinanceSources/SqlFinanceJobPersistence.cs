using System.Data;
using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;

using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.FinanceSources;

public sealed partial class SqlFinanceJobProcessor
{
    private async Task SaveResultAsync(FinanceJobLease lease, FinanceSourceProfile profile, ParsedFinanceFile parsed, CancellationToken ct)
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
        await using (var clear = c.Command($"""
            DELETE {profile.SqlTable} WHERE BatchId=@batch;
            DELETE ingest.ImportIssue WHERE BatchId=@batch;
            DELETE ingest.RawRow WHERE BatchId=@batch;
            """, tx, ("@batch", lease.BatchId))) await clear.ExecuteNonQueryAsync(ct);

        using var raw = new DataTable();
        raw.Columns.Add("BatchId", typeof(long)); raw.Columns.Add("SheetName", typeof(string));
        raw.Columns.Add("RecordNumber", typeof(int)); raw.Columns.Add("CellsJson", typeof(string));
        foreach (var row in parsed.Rows) raw.Rows.Add(lease.BatchId, row.SheetName, row.RecordNumber, PoProfile.Serialize(new FinanceRawPayload(row.Kind,row.Cells)));
        await BulkAsync(c, tx, "ingest.RawRow", raw, ct);
        await using var readIds = c.Command("SELECT RawRowId,RecordNumber,SheetName FROM ingest.RawRow WHERE BatchId=@batch;", tx, ("@batch", lease.BatchId));
        var ids = (await readIds.RowsAsync(ct)).ToDictionary(r => (r.Text("SheetName"), r.Int("RecordNumber")), r => r.Long("RawRowId"));

        using var lines = new DataTable();
        lines.Columns.Add("BatchId", typeof(long)); lines.Columns.Add("RawRowId", typeof(long));
        lines.Columns.Add("SourceCode", typeof(string));
        foreach(var field in profile.Fields)
            lines.Columns.Add(field.Property,field.Kind switch {FinanceFieldKind.Number=>typeof(decimal),FinanceFieldKind.Date=>typeof(DateTime),_=>typeof(string)});
        foreach(var row in parsed.Rows.Where(x=>x.Kind=="DATA"&&x.Line is not null))
        {
            var values=new object[3+profile.Fields.Length];values[0]=lease.BatchId;values[1]=ids[(row.SheetName,row.RecordNumber)];values[2]=profile.SourceCode;
            for(var i=0;i<profile.Fields.Length;i++)
            {var value=row.Line![profile.Fields[i].Property];values[i+3]=value is DateOnly date?date.ToDateTime(TimeOnly.MinValue):value??DBNull.Value;}
            lines.Rows.Add(values);
        }
        await BulkAsync(c,tx,profile.SqlTable,lines,ct);

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
        var status = parsed.Errors == 0 && parsed.DataRows > 0 ? "READY" : "INVALID";
        await using var finish = c.Command("""
            UPDATE ingest.ImportBatch SET Status=@status,TotalRows=@rows,ErrorCount=@errors,WarningCount=@warnings WHERE BatchId=@batch;
            UPDATE ops.ProcessingJob SET Status='SUCCEEDED',CompletedAtUtc=SYSUTCDATETIME(),
                LeaseOwner=NULL,LeaseUntilUtc=NULL,LastErrorCode=NULL WHERE JobId=@job;
            """, tx, ("@status", status), ("@rows", parsed.DataRows), ("@errors", parsed.Errors),
            ("@warnings", parsed.Warnings), ("@batch", lease.BatchId), ("@job", lease.JobId));
        await finish.ExecuteNonQueryAsync(ct);
        await PoSql.AuditAsync(c, tx, null, "FINANCE_VALIDATED", lease.BatchId, Guid.NewGuid(), null,
            new { status, totalRows = parsed.DataRows, errorCount = parsed.Errors, warningCount = parsed.Warnings, jobId = PoProfile.Id(lease.JobId) }, ct);
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
