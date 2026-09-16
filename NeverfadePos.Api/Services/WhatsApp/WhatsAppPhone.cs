using System.Text.RegularExpressions;

namespace NeverfadePos.Api.Services.WhatsApp;

public static partial class WhatsAppPhone
{
    public static string NormalizeIndonesia(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Nomor WhatsApp wajib diisi.",
                nameof(value));
        }

        var digits = NonDigitRegex().Replace(value, string.Empty);

        if (digits.StartsWith("0", StringComparison.Ordinal))
        {
            digits = $"62{digits[1..]}";
        }
        else if (!digits.StartsWith("62", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Gunakan nomor Indonesia 08xx, 62xx, atau +62xx.",
                nameof(value));
        }

        if (digits.Length is < 10 or > 15 ||
            !digits.StartsWith("628", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Nomor WhatsApp tidak valid.",
                nameof(value));
        }

        return digits;
    }

    public static string Mask(string normalized)
    {
        if (normalized.Length <= 7)
        {
            return normalized;
        }

        return $"+{normalized[..4]}••••{normalized[^4..]}";
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex NonDigitRegex();
}
