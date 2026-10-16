using System.Xml;
using System.Xml.Linq;
using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceLogistics.Api.Controllers;

[ApiController]
[Route("api/auth/saml")]
[AllowAnonymous]
public sealed class SamlController(
    SamlSignInService signIn,
    SamlProtocol protocol,
    IdentitySettings settings,
    ILogger<SamlController> logger) : ControllerBase
{
    [HttpGet("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(CancellationToken ct)
    {
        RequireEnabled();
        Response.Headers.CacheControl = "no-store";

        var started = await signIn.StartAsync(
            Request.Cookies[SessionSecrets.CookieName], ct);

        Response.Cookies.Append(
            SamlBrowserState.CookieName,
            started.BrowserSecret,
            SamlBrowserState.Options(started.ExpiresAtUtc));

        return protocol.Redirect(started.Request);
    }

    [HttpPost("acs")]
    [EnableRateLimiting("session")]
    [RequestSizeLimit(262144)]
    [RequestFormLimits(
        ValueLengthLimit = 220000,
        ValueCountLimit = 4)]
    public async Task<IActionResult> Acs(CancellationToken ct)
    {
        RequireEnabled();
        Response.Headers.CacheControl = "no-store";

        try
        {
            if (!Request.HasFormContentType)
                throw InvalidResponse();

            var form = await Request.ReadFormAsync(ct);
            var values = form["SAMLResponse"];

            if (values.Count != 1 ||
                string.IsNullOrWhiteSpace(values[0]) ||
                values[0]!.Length > 220000)
            {
                throw InvalidResponse();
            }

            var encoded = values[0]!;

            // Chỉ lấy request ID để tra cứu.
            // Chưa tin bất kỳ claim nào ở bước này.
            var requestId = ReadRequestId(encoded);

            var result = await signIn.CompleteAsync(
                Request,
                requestId,
                encoded,
                Request.Cookies[SamlBrowserState.CookieName],
                HttpContext.TraceIdentifier,
                ct);

            Response.Cookies.Append(
                SessionSecrets.CookieName,
                result.SessionCookie,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    IsEssential = true,
                    Expires = result.Response.SessionExpiresAtUtc
                });

            SamlBrowserState.Clear(Response);

            // Không đưa JWT/assertion lên URL.
            Response.StatusCode = StatusCodes.Status303SeeOther;
            Response.Headers.Location = "/sso-complete.html";

            return new EmptyResult();
        }
        catch (IdentityFault)
        {
            logger.LogWarning(
                "SAML callback rejected. CorrelationId={CorrelationId}",
                HttpContext.TraceIdentifier);

            throw;
        }
    }

    private void RequireEnabled()
    {
        if (!settings.SamlEnabled)
        {
            throw new IdentityFault(
                404,
                "SAML_DISABLED",
                "Đăng nhập SSO chưa được bật.");
        }
    }

    private static string ReadRequestId(string encoded)
    {
        try
        {
            var bytes = Convert.FromBase64String(encoded);

            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 200000
            });

            var root = XDocument.Load(reader).Root;

            XNamespace p = "urn:oasis:names:tc:SAML:2.0:protocol";

            if (root?.Name != p + "Response")
                throw new FormatException();

            var id = (string?)root.Attribute("InResponseTo");

            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new FormatException();

            return id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw InvalidResponse();
        }
    }

    private static IdentityFault InvalidResponse() =>
        new(
            400,
            "SAML_RESPONSE_INVALID",
            "Phản hồi SSO không hợp lệ.");
}