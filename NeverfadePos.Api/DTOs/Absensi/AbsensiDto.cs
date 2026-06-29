namespace NeverfadePos.Api.DTOs.Absensi;

public sealed class AbsensiDto
{
    public Guid Id { get; set; }

    public Guid KaryawanId { get; set; }

    public string KaryawanNama { get; set; } = string.Empty;

    public string Jabatan { get; set; } = string.Empty;

    // Format: YYYY-MM-DD
    public string Tanggal { get; set; } = string.Empty;

    // Format: HH:mm
    public string? CheckIn { get; set; }

    // Format: HH:mm
    public string? CheckOut { get; set; }
}
