namespace NeverfadePos.Api.DTOs.Settings;

public sealed class SettingsDto
{
    public string NamaToko { get; set; } = string.Empty;
    public string Alamat { get; set; } = string.Empty;
    public string Telepon { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string HeaderStruk { get; set; } = string.Empty;
    public string FooterStruk { get; set; } = string.Empty;
    public bool ShowTax { get; set; }
    public bool ShowPoint { get; set; }
    public decimal DefaultTax { get; set; }
    public int MinStok { get; set; }
    public int PoinRate { get; set; }
}
