using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace FinanceLogistics.Api.Security;

public static class SessionSecrets
{
    public const string CookieName = "__Host-fl-session";
    public static (string Cookie, byte[] Hash) Create(Guid sessionId)
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        return ($"{sessionId:N}.{Base64UrlEncoder.Encode(secret)}", SHA256.HashData(secret));
    }
    public static bool TryRead(string? cookie, out Guid id, out byte[] hash)
    {
        id = default; hash = [];
        if (cookie is null || cookie.Length != 76 || cookie[32] != '.') return false;
        if (!Guid.TryParseExact(cookie[..32], "N", out id)) return false;
        try
        {
            var raw = Base64UrlEncoder.DecodeBytes(cookie[33..]);
            if (raw.Length != 32) return false;
            hash = SHA256.HashData(raw); return true;
        }
        catch (FormatException) { return false; }
    }
    public static bool Matches(byte[] storedHash, byte[] suppliedHash) =>
        storedHash.Length == 32 && suppliedHash.Length == 32
        && CryptographicOperations.FixedTimeEquals(storedHash, suppliedHash);
}
