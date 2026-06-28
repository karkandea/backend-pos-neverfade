using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Transaction;

public sealed class CreateTransactionItemDto
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Nama { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal HargaJual { get; set; }

    [Range(1, int.MaxValue)]
    public int Qty { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Subtotal { get; set; }
}
