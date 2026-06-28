namespace NeverfadePos.Api.DTOs.Transaction;

public sealed class TransactionDto
{
    public Guid Id { get; set; }

    public string NoTrx { get; set; } = string.Empty;

    public DateTime Tanggal { get; set; }

    public string Kasir { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public string CustomerNama { get; set; } = string.Empty;

    public List<TransactionItemDto> Items { get; set; } = new();

    public decimal Subtotal { get; set; }

    public decimal Disc { get; set; }

    public decimal Tax { get; set; }

    public decimal DiscAmt { get; set; }

    public decimal TaxAmt { get; set; }

    public decimal Total { get; set; }

    public string MetodePembayaran { get; set; } = string.Empty;

    public decimal Dibayar { get; set; }

    public decimal Kembalian { get; set; }
}
