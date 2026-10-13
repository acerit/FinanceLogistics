using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;

namespace FinanceLogistics.Infrastructure.Imports;

public sealed class PoDatabase(string connectionString)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(connectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
}

internal static class PoSql
{
    public static SqlCommand Command(this SqlConnection connection, string sql,
        SqlTransaction? transaction = null, params (string Name, object? Value)[] parameters)
    {
        var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 90 };
        foreach (var (name, value) in parameters)
        {
            // Explicit types for the values used by this module; decimal values go through SqlBulkCopy.
            var parameter = new SqlParameter { ParameterName = name, Value = value ?? DBNull.Value };
            parameter.SqlDbType = value switch
            {
                long => SqlDbType.BigInt, int => SqlDbType.Int, bool => SqlDbType.Bit,
                Guid => SqlDbType.UniqueIdentifier, byte[] => SqlDbType.VarBinary,
                DateTime => SqlDbType.DateTime2, _ => SqlDbType.NVarChar
            };
            if (value is string) parameter.Size = -1;
            if (value is byte[] bytes) parameter.Size = bytes.Length;
            command.Parameters.Add(parameter);
        }
        return command;
    }
    public static async Task<List<Dictionary<string, object?>>> RowsAsync(this SqlCommand command, CancellationToken ct)
    {
        var result = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result.Add(row);
        }
        return result;
    }
    public static long Long(this Dictionary<string, object?> row, string key) => Convert.ToInt64(row[key], CultureInfo.InvariantCulture);
    public static int Int(this Dictionary<string, object?> row, string key) => Convert.ToInt32(row[key], CultureInfo.InvariantCulture);
    public static string Text(this Dictionary<string, object?> row, string key) => Convert.ToString(row[key], CultureInfo.InvariantCulture) ?? "";
    public static string? Maybe(this Dictionary<string, object?> row, string key) => row[key] is null ? null : row.Text(key);
    public static byte[] Bytes(this Dictionary<string, object?> row, string key) => (byte[])row[key]!;
    public static DateTime Date(this Dictionary<string, object?> row, string key) => (DateTime)row[key]!;

    public static async Task RequireFinanceAsync(SqlConnection c, SqlTransaction? tx, long actorId, CancellationToken ct)
    {
        await using var cmd = c.Command("""
            SELECT COUNT(*) FROM sec.AppUser u JOIN sec.UserRole r ON r.UserId=u.UserId
            WHERE u.UserId=@actor AND u.IsActive=1 AND r.RoleCode='FINANCE';
            """, tx, ("@actor", actorId));
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) != 1)
            throw new PoImportFault(403, "FINANCE_REQUIRED", "Tài khoản cần có quyền FINANCE đang hoạt động.");
        await using var source = c.Command("""
            SELECT COUNT(*) FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
            WHERE s.SourceCode='PO_UPDATED' AND sc.ScopeCode='FINANCE_SHARED' AND s.IsActive=1
            AND EXISTS(SELECT 1 FROM cfg.ScopeSite ss JOIN cfg.Site si ON si.SiteCode=ss.SiteCode
                       WHERE ss.ScopeCode=sc.ScopeCode AND si.IsActive=1);
            """, tx);
        if (Convert.ToInt32(await source.ExecuteScalarAsync(ct)) != 1)
            throw new PoImportFault(403, "SOURCE_DISABLED", "Nguồn PO_UPDATED không hoạt động.");
    }
    public static async Task AuditAsync(SqlConnection c, SqlTransaction tx, long? actor,
        string action, long entityId, Guid correlationId, object? before, object after, CancellationToken ct)
    {
        await using var cmd = c.Command("""
            INSERT ops.AuditEvent(ActorUserId,ActorKind,ActionCode,EntityType,EntityId,CorrelationId,BeforeJson,AfterJson)
            VALUES(@actor,@kind,@action,'ImportBatch',@id,@correlation,@before,@after);
            """, tx, ("@actor", actor), ("@kind", actor is null ? "SYSTEM" : "USER"), ("@action", action),
            ("@id", PoProfile.Id(entityId)), ("@correlation", correlationId),
            ("@before", before is null ? null : PoProfile.Serialize(before)), ("@after", PoProfile.Serialize(after)));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
