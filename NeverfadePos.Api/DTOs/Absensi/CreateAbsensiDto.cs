using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Absensi;

public sealed class CreateAbsensiDto
{
    [Required]
    public Guid KaryawanId { get; set; }

    // Frontend masih kirim field ini, sementara diabaikan.
    public string? Foto { get; set; }
}
