namespace NeverfadePos.Api.DTOs.Karyawan;

public sealed class KaryawanDto
{
    public Guid Id { get; set; }
    public string Nama { get; set; } = string.Empty;
    public string Jabatan { get; set; } = string.Empty;
    public string Telepon { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Gaji { get; set; }
    public DateOnly TanggalMasuk { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Catatan { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? LinkedUsername { get; set; }
    public bool HasPin { get; set; }
    public DateTime? PinUpdatedAt { get; set; }
}
