using NeverfadePos.Api.Services.WhatsApp;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class WhatsAppPhoneTests
{
    [Theory]
    [InlineData("0812-3456-7890", "6281234567890")]
    [InlineData("+62 812 3456 7890", "6281234567890")]
    [InlineData("6281234567890", "6281234567890")]
    public void NormalizeIndonesia_AcceptsCommonFormats(
        string input,
        string expected)
    {
        Assert.Equal(
            expected,
            WhatsAppPhone.NormalizeIndonesia(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("621234567890")]
    [InlineData("081")]
    public void NormalizeIndonesia_RejectsInvalidNumbers(
        string input)
    {
        Assert.Throws<ArgumentException>(() =>
            WhatsAppPhone.NormalizeIndonesia(input));
    }

    [Fact]
    public void Mask_HidesMiddleDigits()
    {
        Assert.Equal(
            "+6281••••7890",
            WhatsAppPhone.Mask("6281234567890"));
    }
}
