using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;
using System.Xml;
using System.Xml.Linq;
using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Services;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.AspNetCore.Mvc;

namespace FinanceLogistics.Api.Security;

public sealed record VerifiedSamlResponse(
    string RequestId,
    string ResponseId,
    string AssertionId,
    Guid TenantId,
    Guid ObjectId,
    DateTime ExpiresAtUtc);

public sealed class SamlProtocol : IDisposable
{
    private static readonly XNamespace P =
        "urn:oasis:names:tc:SAML:2.0:protocol";

    private static readonly XNamespace A =
        "urn:oasis:names:tc:SAML:2.0:assertion";

    private static readonly XNamespace D =
        "http://www.w3.org/2000/09/xmldsig#";

    private readonly IdentitySettings settings;
    private readonly TimeProvider clock;
    private readonly List<X509Certificate2> certificates = [];

    public SamlProtocol(
        IdentitySettings settings,
        TimeProvider clock,
        IWebHostEnvironment environment)
    {
        this.settings = settings;
        this.clock = clock;

        if (!settings.SamlEnabled)
            return;

        var webRoot = Path.GetFullPath(
            environment.WebRootPath ??
            Path.Combine(environment.ContentRootPath, "wwwroot"));

        foreach (var configuredPath in settings.Saml.SigningCertificatePaths)
        {
            var path = Path.GetFullPath(configuredPath);

            if (path.Equals(webRoot, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(
                    webRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Keep SAML certificates outside wwwroot.");
            }

            var certificate =
                X509CertificateLoader.LoadCertificateFromFile(path);

            using var rsa = certificate.GetRSAPublicKey();

            if (rsa is null || rsa.KeySize < 2048)
            {
                certificate.Dispose();
                throw new InvalidOperationException(
                    "Use an approved RSA signing certificate of at least 2048 bits.");
            }

            certificates.Add(certificate);
        }

        CheckReady();
    }

    public void CheckReady() => _ = Configuration();

    private Saml2Configuration Configuration()
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var active = certificates.Where(c =>
            c.NotBefore.ToUniversalTime() <= now &&
            c.NotAfter.ToUniversalTime() > now).ToList();

        if (!settings.SamlEnabled || active.Count == 0)
            throw new InvalidOperationException(
                "SAML is disabled or has no currently valid signing certificate.");

        return new Saml2Configuration
        {
            Issuer = settings.Saml.SpEntityId,
            AllowedIssuer = settings.Saml.IdpIssuer,

            SingleSignOnDestination =
                new Uri(settings.Saml.SingleSignOnUrl),

            AllowedAudienceUris = [settings.Saml.SpEntityId],
            AudienceRestricted = true,

            SignatureAlgorithm =
                "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",

            SignatureValidationCertificates = active,

            // Trust is established by the approved certificate files above.
            // This does NOT disable XML signature verification.
            CertificateValidationMode = X509CertificateValidationMode.None,
            RevocationMode = X509RevocationMode.NoCheck,

            SignAuthnRequest = false,

            // Replay protection is persisted in SQL by SamlSignInService.
            DetectReplayedTokens = false
        };
    }

    public Saml2AuthnRequest CreateRequest() =>
        new(Configuration())
        {
            AssertionConsumerServiceUrl = new Uri(settings.Saml.AcsUrl)
        };

    public IActionResult Redirect(Saml2AuthnRequest request) =>
        new Saml2RedirectBinding().Bind(request).ToActionResult();

    public VerifiedSamlResponse Verify(
        HttpRequest request,
        string encodedResponse,
        string expectedRequestId)
    {
        try
        {
            if (encodedResponse.Length is < 1 or > 220_000)
                throw new FormatException();

            var bytes = Convert.FromBase64String(encodedResponse);

            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 200_000
            });

            var document = XDocument.Load(
                reader, LoadOptions.PreserveWhitespace);

            var root = document.Root
                ?? throw new FormatException();

            if (root.Name != P + "Response" ||
                root.Descendants(A + "Assertion").Count() != 1)
            {
                throw new FormatException();
            }

            var assertion = One(root, A + "Assertion");

            if (assertion.Elements(D + "Signature").Count() != 1)
                throw new FormatException();

            var ids = root.DescendantsAndSelf()
                .Attributes()
                .Where(x => string.Equals(
                    x.Name.LocalName, "id",
                    StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Value)
                .ToArray();

            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new FormatException();

            // Unbind performs the library's signature/token validation.
            var response = new Saml2AuthnResponse(Configuration());

            new Saml2PostBinding().Unbind(
                request.ToGenericHttpRequest(validate: true),
                response);

            if (response.Status != Saml2StatusCodes.Success)
                throw new FormatException();

            if (Required(root, "InResponseTo") != expectedRequestId ||
                Required(root, "Destination") != settings.Saml.AcsUrl ||
                One(root, A + "Issuer").Value != settings.Saml.IdpIssuer ||
                One(assertion, A + "Issuer").Value != settings.Saml.IdpIssuer)
            {
                throw new FormatException();
            }

            var subject = One(assertion, A + "Subject");
            var confirmation = One(subject, A + "SubjectConfirmation");

            if (Required(confirmation, "Method") !=
                "urn:oasis:names:tc:SAML:2.0:cm:bearer")
            {
                throw new FormatException();
            }

            var data = One(confirmation, A + "SubjectConfirmationData");

            if (Required(data, "InResponseTo") != expectedRequestId ||
                Required(data, "Recipient") != settings.Saml.AcsUrl)
            {
                throw new FormatException();
            }

            var conditions = One(assertion, A + "Conditions");
            var now = clock.GetUtcNow().UtcDateTime;

            CheckNotBefore(conditions, now);
            CheckNotBefore(data, now);

            var conditionsEnd = Time(Required(conditions, "NotOnOrAfter"));
            var subjectEnd = Time(Required(data, "NotOnOrAfter"));

            var expires = conditionsEnd < subjectEnd
                ? conditionsEnd
                : subjectEnd;

            if (expires <= now)
                throw new FormatException();

            const string tenantClaim =
                "http://schemas.microsoft.com/identity/claims/tenantid";

            const string objectClaim =
                "http://schemas.microsoft.com/identity/claims/objectidentifier";

            var tenantValues = response.ClaimsIdentity
                .FindAll(tenantClaim).Select(x => x.Value).ToArray();

            var objectValues = response.ClaimsIdentity
                .FindAll(objectClaim).Select(x => x.Value).ToArray();

            if (tenantValues.Length != 1 ||
                objectValues.Length != 1 ||
                !Guid.TryParse(tenantValues[0], out var tenantId) ||
                !Guid.TryParse(objectValues[0], out var objectId) ||
                tenantId != settings.Saml.TenantId ||
                objectId == Guid.Empty)
            {
                throw new FormatException();
            }

            return new(
                expectedRequestId,
                Required(root, "ID"),
                Required(assertion, "ID"),
                tenantId,
                objectId,
                expires);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Do not expose assertion contents or library exception details.
            throw new IdentityFault(
                400,
                "SAML_RESPONSE_INVALID",
                "Phản hồi SSO không hợp lệ. Vui lòng bắt đầu đăng nhập lại.");
        }
    }

    private static XElement One(XElement parent, XName name)
    {
        var elements = parent.Elements(name).ToArray();

        return elements.Length == 1
            ? elements[0]
            : throw new FormatException();
    }

    private static string Required(XElement element, string name)
    {
        var value = (string?)element.Attribute(name);

        return !string.IsNullOrWhiteSpace(value) && value.Length <= 2048
            ? value
            : throw new FormatException();
    }

    private static DateTime Time(string value) =>
        XmlConvert.ToDateTimeOffset(value).UtcDateTime;

    private static void CheckNotBefore(XElement element, DateTime now)
    {
        var value = (string?)element.Attribute("NotBefore");

        if (value is not null && Time(value) > now.AddMinutes(2))
            throw new FormatException();
    }

    public void Dispose()
    {
        foreach (var certificate in certificates)
            certificate.Dispose();
    }
}