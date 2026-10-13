using FinanceLogistics.Application.Periods;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using FinanceLogistics.Api.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceLogistics.Api.Controllers;

[ApiController]
[Authorize(Policy = "FinanceBusiness")]
[Route("api/periods")]
public sealed class PoPeriodsController(IPoPeriodReader reader, IIdentityStore identityStore) : ControllerBase
{
    // Milestone: this endpoint lists only periods having a PO_UPDATED Finance requirement.
    // Broader multi-source/month administration is a later use case.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PoPeriodDto>>> List(CancellationToken ct)
    {
        await RequirePoReadAsync(ct);
        return Ok(await reader.ListPeriodsAsync(ct));
    }

    [HttpGet("{periodId:int}/requirements")]
    public async Task<ActionResult<IReadOnlyList<PoRequirementDto>>> Requirements(int periodId, CancellationToken ct)
    {
        await RequirePoReadAsync(ct);
        if (periodId <= 0)
            throw new IdentityFault(400, "INVALID_PERIOD", "Kỳ dữ liệu không hợp lệ.");
        var items = await reader.ListRequirementsAsync(periodId, ct);
        if (items.Count == 0)
            throw new IdentityFault(404, "PO_REQUIREMENT_NOT_FOUND", "Không có yêu cầu nộp PO cho kỳ này.");
        return Ok(items);
    }

    private async Task RequirePoReadAsync(CancellationToken ct)
    {
        var source = (await identityStore.SourcesAsync(ct)).SingleOrDefault(x =>
            x.SourceCode == "PO_UPDATED" && x.ScopeCode == "FINANCE_SHARED");
        if (source is null)
            throw new IdentityFault(404, "SOURCE_NOT_FOUND", "Chưa cấu hình nguồn PO_UPDATED.");
        if (!AccessRules.CanAccessSource(CurrentUser.Get(HttpContext), source, "read"))
            throw new IdentityFault(403, "SOURCE_FORBIDDEN", "Bạn không có quyền xem nguồn PO_UPDATED.");
    }
}
