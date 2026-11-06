using FinanceLogistics.Application.LogisticsTrade;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
using Microsoft.Data.SqlClient;
namespace FinanceLogistics.Infrastructure.LogisticsTrade;

internal static class TradeSourceSql
{
    public static async Task RequireSourceAsync(SqlConnection c,SqlTransaction? tx,long actorId,string sourceCode,CancellationToken ct)
    {
        var profile=TradeSourceProfiles.Get(sourceCode);
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
            throw new PoImportFault(403,"TRADE_SOURCE_FORBIDDEN","Nguồn/site không hoạt động hoặc tài khoản không có quyền nghiệp vụ trên site này.");
    }
}
public sealed class TradeImportFactory(PoDatabase database,LocalPoFileStorage files) : ITradeImportFactory
{
    ITradeSourceImportService ITradeImportFactory.For(string sourceCode)=>For(sourceCode);
    public SqlTradeImportService For(string sourceCode)=>new(database,files,TradeSourceProfiles.Get(sourceCode));
}
