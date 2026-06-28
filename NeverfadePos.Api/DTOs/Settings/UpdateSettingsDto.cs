using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Settings;

public sealed class UpdateSettingsDto
{
    [Required]
    [MaxLength(200)]
    public string NamaToko { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Alamat { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Telepon { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Website { get; set; } = string.Empty;

    [MaxLength(500)]
    public string HeaderStruk { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FooterStruk { get; set; } = string.Empty;

    public bool ShowTax { get; set; }

    public bool ShowPoint { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DefaultTax { get; set; }

    [Range(0, int.MaxValue)]
    public int MinStok { get; set; }

    [Range(0, int.MaxValue)]
    public int PoinRate { get; set; }
}
