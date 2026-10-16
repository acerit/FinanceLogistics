using FinanceLogistics.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceLogistics.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth/methods")]
public sealed class AuthMethodsController(
    IdentitySettings settings) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        Response.Headers.CacheControl = "no-store";

        return Ok(new
        {
            ldapEnabled = settings.LdapEnabled,
            samlEnabled = settings.SamlEnabled
        });
    }
}