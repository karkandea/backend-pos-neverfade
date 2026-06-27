using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class Transaction : BaseEntity
{
    public string NoTrx { get; set; } = string.Empty;

    public DateTime Tanggal { get; set; } = DateTime.UtcNow;

    public string Kasir { get; set; } = string.Empty;

    public Guid KasirId { get; set; }

    public Guid? CustomerId { get; set; }

    public string CustomerNama { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal Disc { get; set; }

    public decimal Tax { get; set; }

    public decimal DiscAmt { get; set; }

    public decimal TaxAmt { get; set; }

    public decimal Total { get; set; }

    public string MetodePembayaran { get; set; } = string.Empty;

    public decimal Dibayar { get; set; }

    public decimal Kembalian { get; set; }

    public Tenant? Tenant { get; set; }

    public Customer? Customer { get; set; }

    public ICollection<TransactionItem> Items { get; set; } = new List<TransactionItem>();
}
