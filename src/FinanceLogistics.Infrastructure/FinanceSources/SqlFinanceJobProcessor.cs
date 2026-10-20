using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using FinanceLogistics.Application.Imports;

using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.FinanceSources;

internal sealed record FinanceJobLease(long JobId, long BatchId, string Owner, int Attempt);

public sealed partial class SqlFinanceJobProcessor(PoDatabase database, LocalPoFileStorage files,
    FinanceFileParser parser, FinanceImportFactory factory, ILogger<SqlFinanceJobProcessor> logger)
{
    public async Task<bool> TryProcessNextAsync(CancellationToken ct)
    {
        var lease = await ClaimAsync(ct);
        if (lease is null) return false;
        using var work = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = HeartbeatAsync(lease, work, heartbeatStop.Token);
        try
        {
            await using var c = await database.OpenAsync(work.Token);
            // A reclaimed job may find VALIDATING. No worker changes READY/COMMITTED data.
            await using (var update = c.Command("""
                UPDATE ingest.ImportBatch SET Status='VALIDATING'
                WHERE BatchId=@batch AND Status IN('RECEIVED','VALIDATING')
                  AND EXISTS(SELECT 1 FROM ops.ProcessingJob WHERE JobId=@job AND Status='RUNNING'
                    AND LeaseOwner=@owner AND AttemptCount=@attempt AND LeaseUntilUtc>SYSUTCDATETIME());
                """, null, ("@batch", lease.BatchId), ("@job", lease.JobId), ("@owner", lease.Owner), ("@attempt", lease.Attempt)))
                if (await update.ExecuteNonQueryAsync(work.Token) != 1) throw new InvalidOperationException("Job state/lease changed.");
            await using var identify=c.Command("SELECT SourceCode FROM ingest.ImportBatch WHERE BatchId=@batch;",null,("@batch",lease.BatchId));
            var source=Convert.ToString(await identify.ExecuteScalarAsync(work.Token))??"";
            var profile=FinanceSourceProfiles.Get(source);
            var batch=await factory.For(source).BatchAsync(c,lease.BatchId,work.Token);
            var policy=FinanceSourcePolicy.Frozen(batch.Text("ParsingOptionsJson"),profile);
            await using var stream = await files.OpenReadAsync(batch.Text("StorageKey"), work.Token);
            var hash = await SHA256.HashDataAsync(stream, work.Token);
            ParsedFinanceFile parsed;
            if (!CryptographicOperations.FixedTimeEquals(hash, batch.Bytes("Sha256")))
            {
                parsed = new();
                parsed.Issues.Add(new("ERROR", "SOURCE_HASH_MISMATCH", null, "Nội dung file trong kho đã thay đổi so với lúc nộp. Cần kiểm tra kho file."));
            }
            else
            {
                stream.Position = 0;
                parsed = await Task.Run(() => parser.Parse(stream, batch.Text("OriginalName"), source, policy, work.Token), work.Token);
            }
            work.Token.ThrowIfCancellationRequested();
            await SaveResultAsync(lease, profile, parsed, work.Token);
            logger.LogInformation("Finance job {JobId}, batch {BatchId}: {Status}, rows {Rows}, errors {Errors}, warnings {Warnings}",
                lease.JobId, lease.BatchId, parsed.Errors == 0 ? "READY" : "INVALID", parsed.DataRows, parsed.Errors, parsed.Warnings);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Leave durable RUNNING job for lease recovery after a clean/crashed worker shutdown.
        }
        catch (Exception ex)
        {
            var code = ex switch { FileNotFoundException => "SOURCE_FILE_MISSING", UnauthorizedAccessException => "STORAGE_ACCESS_DENIED",
                SqlException => "SQL_PROCESSING_ERROR", IOException => "STORAGE_IO_ERROR",
                OperationCanceledException => "JOB_LEASE_LOST", _ => "PROCESSING_ERROR" };
            logger.LogWarning("Finance job {JobId}, batch {BatchId} failed: {Code}; exception type {ExceptionType}",
                lease.JobId, lease.BatchId, code, ex.GetType().Name);
            try { await FailAsync(lease, code, ct); }
            catch (Exception failure) when (!ct.IsCancellationRequested)
            { logger.LogWarning("Could not persist job failure {JobId}: {ExceptionType}. Lease recovery will retry.", lease.JobId, failure.GetType().Name); }
        }
        finally
        {
            heartbeatStop.Cancel();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
        return true;
    }

    private async Task<FinanceJobLease?> ClaimAsync(CancellationToken ct)
    {
        await using var c = await database.OpenAsync(ct);
        var owner = Guid.NewGuid().ToString("N");
        await using var cmd = c.Command("""
            UPDATE ops.ProcessingJob SET Status='FAILED',LastErrorCode='LEASE_RETRIES_EXHAUSTED',
                LeaseOwner=NULL,LeaseUntilUtc=NULL,CompletedAtUtc=SYSUTCDATETIME()
            WHERE JobType='FINANCE_VALIDATE' AND Status='RUNNING' AND LeaseUntilUtc<=SYSUTCDATETIME() AND AttemptCount>=MaxAttempts;
            ;WITH nextJob AS (
                SELECT TOP(1) * FROM ops.ProcessingJob WITH(UPDLOCK,READPAST,READCOMMITTEDLOCK)
                WHERE JobType='FINANCE_VALIDATE' AND AttemptCount<MaxAttempts
                  AND ((Status='QUEUED' AND AvailableAtUtc<=SYSUTCDATETIME())
                    OR (Status='RUNNING' AND LeaseUntilUtc<=SYSUTCDATETIME()))
                ORDER BY AvailableAtUtc,JobId
            )
            UPDATE nextJob SET Status='RUNNING',AttemptCount=AttemptCount+1,LeaseOwner=@owner,
                LeaseUntilUtc=DATEADD(second,120,SYSUTCDATETIME()),LastErrorCode=NULL,CompletedAtUtc=NULL
            OUTPUT inserted.JobId,inserted.PayloadJson,inserted.AttemptCount;
            """, null, ("@owner", owner));
        var row = (await cmd.RowsAsync(ct)).SingleOrDefault();
        if (row is null) return null;
        using var payload = JsonDocument.Parse(row.Text("PayloadJson"));
        return new(row.Long("JobId"), long.Parse(payload.RootElement.GetProperty("batchId").GetString()!, CultureInfo.InvariantCulture), owner, row.Int("AttemptCount"));
    }
    private async Task HeartbeatAsync(FinanceJobLease lease, CancellationTokenSource work, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await using var c = await database.OpenAsync(ct);
                await using var cmd = c.Command("""
                    UPDATE ops.ProcessingJob SET LeaseUntilUtc=DATEADD(second,120,SYSUTCDATETIME())
                    WHERE JobId=@job AND Status='RUNNING' AND LeaseOwner=@owner AND AttemptCount=@attempt
                      AND LeaseUntilUtc>SYSUTCDATETIME();
                    """, null, ("@job", lease.JobId), ("@owner", lease.Owner), ("@attempt", lease.Attempt));
                if (await cmd.ExecuteNonQueryAsync(ct) != 1) { work.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogWarning("Finance lease heartbeat stopped for job {JobId}: {ExceptionType}", lease.JobId, ex.GetType().Name);
            work.Cancel();
        }
    }
    private async Task FailAsync(FinanceJobLease lease, string code, CancellationToken ct)
    {
        await using var c = await database.OpenAsync(ct);
        await using var cmd = c.Command("""
            UPDATE ops.ProcessingJob SET Status=CASE WHEN AttemptCount>=MaxAttempts THEN 'FAILED' ELSE 'QUEUED' END,
                AvailableAtUtc=DATEADD(second,15*AttemptCount,SYSUTCDATETIME()),LastErrorCode=@code,
                CompletedAtUtc=CASE WHEN AttemptCount>=MaxAttempts THEN SYSUTCDATETIME() ELSE NULL END,
                LeaseOwner=NULL,LeaseUntilUtc=NULL
            WHERE JobId=@job AND Status='RUNNING' AND LeaseOwner=@owner AND AttemptCount=@attempt
              AND LeaseUntilUtc>SYSUTCDATETIME();
            """, null, ("@job", lease.JobId), ("@owner", lease.Owner), ("@attempt", lease.Attempt), ("@code", code));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
