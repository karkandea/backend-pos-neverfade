using System.Security.Cryptography;
using System.Text;

namespace NeverfadePos.Api.Services.SharedPos;

internal sealed class SharedPosSecurity(IConfiguration configuration)
{
    private readonly byte[] _pinFingerprintKey = SHA256.HashData(
        Encoding.UTF8.GetBytes(
            "neverfade-shared-pos-pin:" +
            (configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Jwt:Key missing."))));

    public static string GenerateOpaqueToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string HashToken(string token) =>
        Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    public string FingerprintPin(Guid tenantId, string pin)
    {
        using var hmac = new HMACSHA256(_pinFingerprintKey);
        var data = Encoding.UTF8.GetBytes($"{tenantId:N}:{pin}");
        return Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
    }

    public static string HashPin(string pin) =>
        BCrypt.Net.BCrypt.HashPassword(pin, workFactor: 12);

    public static bool VerifyPin(string pin, string hash) =>
        BCrypt.Net.BCrypt.Verify(pin, hash);
}
