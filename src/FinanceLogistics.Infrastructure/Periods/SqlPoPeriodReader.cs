using System.Data;
using System.Globalization;
using FinanceLogistics.Application.Periods;
using FinanceLogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceLogistics.Infrastructure.Periods;

// Parameterized ADO.NET read queries over the existing EF connection.
// DbContext owns/disposes the connection; each request uses its own scoped context.
// All source/scope filtering is server-defined. The API must authorize PO access first.
public sealed class SqlPoPeriodReader(ImportDbContext db) : IPoPeriodReader
{
    public async Task<IReadOnlyList<PoPeriodDto>> ListPeriodsAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = """
            SELECT p.PeriodId,p.PeriodCode,p.StartDate,p.EndDateExclusive,p.Status
            FROM cfg.ReportingPeriod p
            WHERE EXISTS(
                SELECT 1 FROM ingest.SubmissionRequirement r
                WHERE r.PeriodId=p.PeriodId
                  AND r.SourceCode='PO_UPDATED' AND r.ScopeCode='FINANCE_SHARED'
            )
            ORDER BY p.StartDate DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<PoPeriodDto>();
        while (await reader.ReadAsync(ct))
            result.Add(new(
                reader.GetInt32(0).ToString(CultureInfo.InvariantCulture),
                reader.GetString(1),
                DateOnly.FromDateTime(reader.GetDateTime(2)),
                DateOnly.FromDateTime(reader.GetDateTime(3)),
                reader.GetString(4)));
        return result;
    }

    public async Task<IReadOnlyList<PoRequirementDto>> ListRequirementsAsync(int periodId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = """
            SELECT r.RequirementId,r.PeriodId,r.SourceCode,r.ScopeCode,
                   r.DueAtUtc,r.TimeZoneId,r.IsRequired,r.ResponsibleUserId,
                   r.ActiveBatchId,b.VersionNo,b.Status,p.Status,r.RowVersion
            FROM ingest.SubmissionRequirement r
            JOIN cfg.ReportingPeriod p ON p.PeriodId=r.PeriodId
            LEFT JOIN ingest.ImportBatch b
              ON b.BatchId=r.ActiveBatchId AND b.RequirementId=r.RequirementId
            WHERE r.PeriodId=@periodId
              AND r.SourceCode='PO_UPDATED' AND r.ScopeCode='FINANCE_SHARED'
            ORDER BY r.RequirementId;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@periodId";
        parameter.DbType = DbType.Int32;
        parameter.Value = periodId;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<PoRequirementDto>();
        while (await reader.ReadAsync(ct))
        {
            DateTimeOffset? due = reader.IsDBNull(4) ? null
                : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc));
            result.Add(new(
                reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
                reader.GetString(2), reader.GetString(3),
                due, reader.GetString(5), reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetInt64(7).ToString(CultureInfo.InvariantCulture),
                reader.IsDBNull(8) ? null : reader.GetInt64(8).ToString(CultureInfo.InvariantCulture),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetString(11),
                Convert.ToBase64String(reader.GetFieldValue<byte[]>(12))));
        }
        return result;
    }
}
