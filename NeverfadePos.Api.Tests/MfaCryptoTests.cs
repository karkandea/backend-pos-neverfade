using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using NeverfadePos.Api.Services.Mfa;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class MfaCryptoTests
{
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Rfc6238_HmacSha1_SixDigitVectors(long unixSeconds, string expected)
    {
        var key = Encoding.ASCII.GetBytes("12345678901234567890");
        var step = unixSeconds / 30;
        Assert.Equal(expected, MfaTotp.AtStep(key, step));
        var now = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        Assert.Equal(step, MfaTotp.Match(key, expected, now));
        Assert.Null(MfaTotp.Match(key, expected, now, step));
    }

    [Fact]
    public void InvalidOtpIsRejected()
    {
        var key = MfaTotp.GenerateSecret();
        Assert.Equal(32, MfaTotp.EncodeBase32(key).Length);
        Assert.Null(MfaTotp.Match(key, "12345X", DateTime.UtcNow));
        Assert.Null(MfaTotp.Match(key, "12345", DateTime.UtcNow));
    }

    [Fact]
    public void SecretCannotBeDecryptedAsAnotherUser()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mfa:EncryptionKey"] = Convert.ToBase64String(
                    Enumerable.Range(1, 32).Select(x => (byte)x).ToArray())
            }).Build();
        var protector = new MfaSecretProtector(config);
        var userId = Guid.NewGuid();
        var secret = MfaTotp.GenerateSecret();
        var ciphertext = protector.Protect(secret, userId);
        Assert.Equal(secret, protector.Unprotect(ciphertext, userId));
        Assert.ThrowsAny<CryptographicException>(
            () => protector.Unprotect(ciphertext, Guid.NewGuid()));
    }
}
