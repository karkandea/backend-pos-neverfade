using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NeverfadePos.Api.Services.Mfa;

public static class MfaTotp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] GenerateSecret() => RandomNumberGenerator.GetBytes(20);

    public static string EncodeBase32(ReadOnlySpan<byte> bytes)
    {
        var result = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var item in bytes)
        {
            buffer = (buffer << 8) | item;
            bits += 8;
            while (bits >= 5)
                result.Append(Alphabet[(buffer >> (bits -= 5)) & 31]);
        }
        if (bits > 0)
            result.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return result.ToString();
    }

    public static string AtStep(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> digest = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, digest);
        var offset = digest[19] & 15;
        var number = ((digest[offset] & 127) << 24) |
                     (digest[offset + 1] << 16) |
                     (digest[offset + 2] << 8) |
                     digest[offset + 3];
        return (number % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public static long? Match(ReadOnlySpan<byte> secret, string? code,
        DateTime utcNow, long lastUsedStep = -1)
    {
        if (code is null || code.Length != 6 ||
            code.Any(x => x < '0' || x > '9'))
            return null;
        var supplied = Encoding.ASCII.GetBytes(code);
        var step = new DateTimeOffset(utcNow.ToUniversalTime())
            .ToUnixTimeSeconds() / 30;
        for (var drift = -1; drift <= 1; drift++)
        {
            var candidate = step + drift;
            if (candidate <= lastUsedStep) continue;
            var expected = Encoding.ASCII.GetBytes(AtStep(secret, candidate));
            if (CryptographicOperations.FixedTimeEquals(supplied, expected))
                return candidate;
        }
        return null;
    }
}

public sealed class MfaSecretProtector(IConfiguration configuration)
{
    private byte[] Key()
    {
        var input = configuration["Mfa:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(input))
            throw new InvalidOperationException("MFA encryption key unavailable.");
        byte[] key;
        try { key = Convert.FromBase64String(input); }
        catch (FormatException)
        {
            throw new InvalidOperationException("Invalid MFA encryption key format.");
        }
        if (key.Length != 32)
            throw new InvalidOperationException("MFA encryption key must contain 32 bytes.");
        return key;
    }

    public bool Configured
    {
        get
        {
            if (string.IsNullOrWhiteSpace(configuration["Mfa:EncryptionKey"]))
                return false;
            _ = Key();
            return true;
        }
    }

    public string Protect(byte[] secret, Guid userId)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[secret.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(Key(), 16))
            aes.Encrypt(nonce, secret, ciphertext, tag, userId.ToByteArray());
        return Convert.ToBase64String(nonce.Concat(tag).Concat(ciphertext).ToArray());
    }

    public byte[] Unprotect(string value, Guid userId)
    {
        var packed = Convert.FromBase64String(value);
        if (packed.Length < 29)
            throw new CryptographicException("Invalid MFA secret.");
        var plaintext = new byte[packed.Length - 28];
        using var aes = new AesGcm(Key(), 16);
        aes.Decrypt(packed.AsSpan(0, 12), packed.AsSpan(28),
            packed.AsSpan(12, 16), plaintext, userId.ToByteArray());
        return plaintext;
    }
}
