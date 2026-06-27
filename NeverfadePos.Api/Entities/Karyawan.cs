using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class Karyawan : BaseEntity
{
    public string Nama { get; set; } = string.Empty;
    public string Jabatan { get; set; } = string.Empty;
    public string Telepon { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Gaji { get; set; }
    public DateOnly TanggalMasuk { get; set; }
    public string Status { get; set; } = "aktif";
    public string Catatan { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }

    public ICollection<Absensi> Absensis { get; set; } = new List<Absensi>();
}
