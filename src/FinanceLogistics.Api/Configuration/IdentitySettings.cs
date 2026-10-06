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
        Directories = new Dictionary<string, DirectorySettings>(Directories, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class DirectorySettings
{
    public string DomainName { get; set; } = "";
    public string NetBiosName { get; set; } = "";
    public string? SearchBaseDn { get; set; }
}
