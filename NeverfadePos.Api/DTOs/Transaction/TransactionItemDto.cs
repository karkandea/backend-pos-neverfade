namespace NeverfadePos.Api.DTOs.Transaction;

public sealed class TransactionItemDto
{
    public Guid Id { get; set; }

    public string Nama { get; set; } = string.Empty;

    public decimal HargaJual { get; set; }

    public int Qty { get; set; }

    public decimal Subtotal { get; set; }
}
