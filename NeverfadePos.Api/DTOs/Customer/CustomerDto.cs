namespace NeverfadePos.Api.DTOs.Customer;

public sealed class CustomerDto
{
    public Guid Id { get; set; }

    public string Nama { get; set; } = string.Empty;

    public string Hp { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Alamat { get; set; } = string.Empty;

    public int Poin { get; set; }

    public int TotalTransaksi { get; set; }

    public DateTime CreatedAt { get; set; }
}
