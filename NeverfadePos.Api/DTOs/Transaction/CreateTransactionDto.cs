using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Transaction;

public sealed class CreateTransactionDto
{
    public Guid? CustomerId { get; set; }

    [MinLength(1)]
    public List<CreateTransactionItemDto> Items { get; set; } = new();

    [Range(0, double.MaxValue)]
    public decimal Subtotal { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Disc { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Tax { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DiscAmt { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxAmt { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Total { get; set; }

    [Required]
    [MaxLength(50)]
    public string MetodePembayaran { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Dibayar { get; set; }

    public decimal Kembalian { get; set; }
}
