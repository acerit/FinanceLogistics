using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using FinanceLogistics.Api.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceLogistics.Api.Controllers;

[ApiController, Route("api/auth"), AllowAnonymous]
[ServiceFilter(typeof(SessionCsrfFilter))]
public sealed class AuthController(AuthService auth, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken, headerName = "X-CSRF-TOKEN" });
    }

    [HttpPost("login"), EnableRateLimiting("login"), RequestSizeLimit(8192)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, Request.Cookies[SessionSecrets.CookieName], HttpContext.TraceIdentifier, ct);
        Response.Cookies.Append(SessionSecrets.CookieName, result.SessionCookie,
            CookieOptions(result.Response.SessionExpiresAtUtc));
        return Ok(result.Response);
    }

    [HttpPost("token"), EnableRateLimiting("session")]
    public async Task<ActionResult<AuthResponse>> Resume(CancellationToken ct)
    {
        try { return Ok(await auth.ResumeAsync(Request.Cookies[SessionSecrets.CookieName], ct)); }
        catch (IdentityFault e) when (e.Status is 401 or 403)
        {
            ClearCookie(); throw;
        }
    }

    [HttpPost("logout"), EnableRateLimiting("session")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[SessionSecrets.CookieName], HttpContext.TraceIdentifier, ct);
        ClearCookie();
        return NoContent();
    }

    private void ClearCookie() => Response.Cookies.Delete(SessionSecrets.CookieName, CookieOptions(null));
    private static CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/",
        IsEssential = true, Expires = expires
    };
}
