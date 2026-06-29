namespace NeverfadePos.Api.DTOs.Absensi;

public sealed class AbsensiResultDto
{
    public bool Ok { get; set; }

    // Format HH:mm. Akan terisi sesuai endpoint yang dipanggil.
    public string? CheckIn { get; set; }

    // Format HH:mm. Akan terisi sesuai endpoint yang dipanggil.
    public string? CheckOut { get; set; }

    // Sesuai contract saat ini selalu null (foto diabaikan).
    public string? FotoUrl { get; set; }
}
