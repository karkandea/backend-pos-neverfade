using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Customer;

public sealed class UpdateCustomerDto
{
    [Required]
    [MaxLength(200)]
    public string Nama { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Hp { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Alamat { get; set; } = string.Empty;
}
