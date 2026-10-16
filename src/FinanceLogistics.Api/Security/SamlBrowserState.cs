using System.Security.Cryptography;

namespace FinanceLogistics.Api.Security;

public static class SamlBrowserState
{
    public const string CookieName = "__Host-fl-saml-flow";

    public static string CreateSecret() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string secret) =>
        SHA256.HashData(Convert.FromHexString(secret));

    public static bool Matches(string? secret, byte[] expectedHash)
    {
        if (secret is null || secret.Length != 64)
            return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Hash(secret), expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static CookieOptions Options(DateTimeOffset? expires = null) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            IsEssential = true,
            Expires = expires
        };

    public static void Clear(HttpResponse response) =>
        response.Cookies.Delete(CookieName, Options());
}