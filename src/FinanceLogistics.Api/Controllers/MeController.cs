using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using FinanceLogistics.Api.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceLogistics.Api.Controllers;

[ApiController, Authorize]
public sealed class MeController(IIdentityStore store) : ControllerBase
{
    [HttpGet("api/me")]
    public ActionResult<UserProfile> Me() => Ok(CurrentUser.Get(HttpContext));

    [HttpGet("api/me/source-access")]
    public async Task<ActionResult<SourceAccessResponse[]>> MySources(CancellationToken ct)
    {
        var user = CurrentUser.Get(HttpContext);
        var allowed = (await store.SourcesAsync(ct)).Where(x => AccessRules.CanAccessSource(user, x))
            .Select(x => new SourceAccessResponse(x.SourceCode, x.ScopeCode, x.Sites));
        return Ok(allowed);
    }

    [HttpGet("api/source-access/{sourceCode}/{scopeCode}")]
    public async Task<ActionResult<SourceAccessResponse>> Source(string sourceCode, string scopeCode, CancellationToken ct)
    {
        var source = (await store.SourcesAsync(ct)).SingleOrDefault(x => x.SourceCode == sourceCode && x.ScopeCode == scopeCode);
        if (source is null) throw new IdentityFault(404, "SOURCE_NOT_FOUND", "Không tìm thấy nguồn/phạm vi dữ liệu.");
        if (!AccessRules.CanAccessSource(CurrentUser.Get(HttpContext), source))
            throw new IdentityFault(403, "SOURCE_FORBIDDEN", "Bạn không có quyền truy cập nguồn dữ liệu này.");
        return Ok(new SourceAccessResponse(source.SourceCode, source.ScopeCode, source.Sites));
    }
}
