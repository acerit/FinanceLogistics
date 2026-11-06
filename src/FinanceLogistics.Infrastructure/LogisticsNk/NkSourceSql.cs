using FinanceLogistics.Application.LogisticsNk;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
using Microsoft.Data.SqlClient;
namespace FinanceLogistics.Infrastructure.LogisticsNk;

internal static class NkSourceSql
{
    public static async Task RequireSourceAsync(SqlConnection c,SqlTransaction? tx,long actorId,string sourceCode,CancellationToken ct)
    {
        var profile=NkSourceProfiles.Get(sourceCode);
        await using var cmd=c.Command("""
            SELECT COUNT(*) FROM cfg.SourceType s JOIN cfg.SourceScope sc ON sc.SourceCode=s.SourceCode
            WHERE s.SourceCode=@source AND s.OwnerRoleCode='LOGISTICS' AND s.IsActive=1 AND sc.ScopeCode=@scope
              AND EXISTS(SELECT 1 FROM cfg.ScopeSite ss JOIN cfg.Site si ON si.SiteCode=ss.SiteCode
                         WHERE ss.ScopeCode=@scope AND ss.SiteCode=@site AND si.IsActive=1)
              AND NOT EXISTS(SELECT 1 FROM cfg.ScopeSite ss LEFT JOIN cfg.Site si ON si.SiteCode=ss.SiteCode
                         WHERE ss.ScopeCode=@scope AND (ss.SiteCode<>@site OR si.SiteCode IS NULL OR si.IsActive=0))
              AND EXISTS(SELECT 1 FROM sec.AppUser u WHERE u.UserId=@actor AND u.IsActive=1 AND (
                EXISTS(SELECT 1 FROM sec.UserRole ur WHERE ur.UserId=u.UserId AND ur.RoleCode='FINANCE')
                OR (EXISTS(SELECT 1 FROM sec.UserRole ur WHERE ur.UserId=u.UserId AND ur.RoleCode='LOGISTICS')
                    AND NOT EXISTS(SELECT 1 FROM cfg.ScopeSite ss WHERE ss.ScopeCode=@scope AND NOT EXISTS(
                        SELECT 1 FROM sec.UserSite us WHERE us.UserId=u.UserId AND us.SiteCode=ss.SiteCode)))
              ));
            """,tx,("@source",sourceCode),("@scope",profile.ScopeCode),("@site",profile.SiteCode),("@actor",actorId));
        if(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))!=1)
            throw new PoImportFault(403,"NK_SOURCE_FORBIDDEN","Nguồn/site không hoạt động hoặc tài khoản không có quyền nghiệp vụ trên site này.");
    }
}
public sealed class NkImportFactory(PoDatabase database,LocalPoFileStorage files) : INkImportFactory
{
    INkSourceImportService INkImportFactory.For(string sourceCode)=>For(sourceCode);
    public SqlNkImportService For(string sourceCode)=>new(database,files,NkSourceProfiles.Get(sourceCode));
}
