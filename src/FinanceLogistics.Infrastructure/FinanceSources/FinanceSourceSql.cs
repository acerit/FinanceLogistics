using FinanceLogistics.Application.FinanceSources;
using Microsoft.Data.SqlClient;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.FinanceSources;
internal static class FinanceSourceSql
{
    public static async Task RequireFinanceAsync(SqlConnection c,SqlTransaction? tx,long actorId,string sourceCode,CancellationToken ct)
    {
        FinanceSourceProfiles.Get(sourceCode);
        await using var cmd=c.Command("""
            SELECT COUNT(*) FROM sec.AppUser u JOIN sec.UserRole r ON r.UserId=u.UserId
            WHERE u.UserId=@actor AND u.IsActive=1 AND r.RoleCode='FINANCE';
            """,tx,("@actor",actorId));
        if(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))!=1)throw new PoImportFault(403,"FINANCE_REQUIRED","Tài khoản cần quyền FINANCE đang hoạt động.");
        await using var source=c.Command("""
            SELECT COUNT(*) FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
            WHERE s.SourceCode=@source AND s.OwnerRoleCode='FINANCE' AND s.IsActive=1 AND sc.ScopeCode='FINANCE_SHARED'
              AND EXISTS(SELECT 1 FROM cfg.ScopeSite ss WHERE ss.ScopeCode=sc.ScopeCode)
              AND NOT EXISTS(SELECT 1 FROM cfg.ScopeSite ss LEFT JOIN cfg.Site si ON si.SiteCode=ss.SiteCode
                WHERE ss.ScopeCode=sc.ScopeCode AND (si.SiteCode IS NULL OR si.IsActive=0));
            """,tx,("@source",sourceCode));
        if(Convert.ToInt32(await source.ExecuteScalarAsync(ct))!=1)throw new PoImportFault(403,"SOURCE_DISABLED","Nguồn/phạm vi Finance chưa hoạt động.");
    }
}
public sealed class FinanceImportFactory(PoDatabase database,LocalPoFileStorage files) : IFinanceImportFactory
{
    IFinanceSourceImportService IFinanceImportFactory.For(string sourceCode)=>For(sourceCode);
    public SqlFinanceImportService For(string sourceCode)=>new(database,files,FinanceSourceProfiles.Get(sourceCode));
}
