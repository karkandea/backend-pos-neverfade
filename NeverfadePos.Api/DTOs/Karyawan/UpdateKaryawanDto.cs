using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Karyawan;

public sealed class UpdateKaryawanDto
{
    [Required]
    [MaxLength(200)]
    public string Nama { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Jabatan { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Telepon { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Gaji { get; set; }

    public DateOnly TanggalMasuk { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Catatan { get; set; } = string.Empty;
}
