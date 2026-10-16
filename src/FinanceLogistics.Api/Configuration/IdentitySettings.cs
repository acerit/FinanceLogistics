namespace FinanceLogistics.Api.Configuration;

public sealed class IdentitySettings
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKeyBase64 { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 10;
    public int SessionMinutes { get; set; } = 120;
    public string KeyRingPath { get; set; } = "";
    public int LoginRequestsPerFiveMinutesPerIp { get; set; } = 10;
    public Dictionary<string, DirectorySettings> Directories { get; set; } = new();
    public string AuthenticationMode { get; set; } = "Ldap";
    public SamlSettings Saml { get; set; } = new();

    public bool LdapEnabled =>
        AuthenticationMode is "Ldap" or "SamlPilot";

    public bool SamlEnabled =>
        AuthenticationMode is "SamlPilot" or "Saml";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Set Identity Issuer and Audience.");
        byte[] key;
        try { key = Convert.FromBase64String(SigningKeyBase64); }
        catch (FormatException) { throw new InvalidOperationException("SigningKeyBase64 must be valid base64."); }
        if (key.Length < 32)
            throw new InvalidOperationException("Use a randomly generated signing key of at least 32 bytes.");
        if (AccessTokenMinutes is < 1 or > 30 || SessionMinutes < AccessTokenMinutes || SessionMinutes > 480)
            throw new InvalidOperationException("Invalid token/session lifetime.");
        if (!Path.IsPathFullyQualified(KeyRingPath))
            throw new InvalidOperationException("Set an absolute Identity:KeyRingPath outside wwwroot.");
        if (LoginRequestsPerFiveMinutesPerIp is < 1 or > 100)
            throw new InvalidOperationException("Invalid login rate limit.");
        if (Directories.Count == 0 || Directories.Values.Any(d =>
            string.IsNullOrWhiteSpace(d.DomainName) || string.IsNullOrWhiteSpace(d.NetBiosName)))
            throw new InvalidOperationException("Configure the allowed AD directories.");
        if (AuthenticationMode is not ("Ldap" or "SamlPilot" or "Saml"))
            throw new InvalidOperationException("Identity:AuthenticationMode must be Ldap, SamlPilot or Saml.");

        Directories ??= new();

        if (LdapEnabled && (Directories.Count == 0 || Directories.Values.Any(d => string.IsNullOrWhiteSpace(d.DomainName) || string.IsNullOrWhiteSpace(d.NetBiosName))))
        {
            throw new InvalidOperationException("Configure the allowed AD directories.");
        }

        Directories = new Dictionary<string, DirectorySettings>(Directories, StringComparer.OrdinalIgnoreCase);

        Saml ??= new();

        if (SamlEnabled) Saml.Validate();
    }
}

public sealed class DirectorySettings
{
    public string DomainName { get; set; } = "";
    public string NetBiosName { get; set; } = "";
    public string? SearchBaseDn { get; set; }
}

public sealed class SamlSettings
{
    public string ProviderKey { get; set; } = "ENTRA_CORPORATE";
    public Guid TenantId { get; set; }

    public string SpEntityId { get; set; } = "";
    public string AcsUrl { get; set; } = "";

    public string IdpIssuer { get; set; } = "";
    public string SingleSignOnUrl { get; set; } = "";

    public string[] SigningCertificatePaths { get; set; } = [];
    public int RequestMinutes { get; set; } = 10;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProviderKey) ||
            ProviderKey.Length > 40 ||
            TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Configure the SAML provider and TenantId.");
        }

        if (SpEntityId.Length > 1024 ||
            !Uri.TryCreate(SpEntityId, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "Configure an absolute SAML SpEntityId.");
        }

        if (!Uri.TryCreate(AcsUrl, UriKind.Absolute, out var acs) ||
            acs.Scheme != Uri.UriSchemeHttps ||
            acs.AbsolutePath != "/api/auth/saml/acs" ||
            acs.Query.Length != 0 ||
            acs.Fragment.Length != 0 ||
            acs.UserInfo.Length != 0)
        {
            throw new InvalidOperationException(
                "AcsUrl must be HTTPS and end with /api/auth/saml/acs.");
        }

        // V1 dành cho Microsoft Entra public cloud.
        var expectedIssuer = $"https://sts.windows.net/{TenantId:D}/";
        var expectedLogin =
            $"https://login.microsoftonline.com/{TenantId:D}/saml2";

        if (!string.Equals(IdpIssuer, expectedIssuer,
                StringComparison.Ordinal) ||
            !string.Equals(SingleSignOnUrl, expectedLogin,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "SAML issuer/login URL must match the configured Entra tenant.");
        }

        if (SigningCertificatePaths is null ||
            SigningCertificatePaths.Length == 0 ||
            SigningCertificatePaths.Any(
                p => !Path.IsPathFullyQualified(p)))
        {
            throw new InvalidOperationException(
                "Configure absolute paths to approved IdP signing certificates.");
        }

        if (RequestMinutes is < 2 or > 15)
            throw new InvalidOperationException(
                "SAML RequestMinutes must be between 2 and 15.");
    }
}