using FinanceLogistics.Api.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinanceLogistics.Api.Web;

// Cookie-backed login/session/logout are protected, including login CSRF.
public sealed class SessionCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method)) return;
        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
            context.Result = new BadRequestObjectResult(new ApiError("CSRF_INVALID",
                "Phiên bảo vệ yêu cầu đã thay đổi. Vui lòng tải lại trang.", context.HttpContext.TraceIdentifier));
    }
}
