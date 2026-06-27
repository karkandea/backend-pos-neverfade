using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class TransactionItem : BaseEntity
{
    public Guid TransactionId { get; set; }

    public Guid ProductId { get; set; }

    public string Nama { get; set; } = string.Empty;

    public decimal HargaJual { get; set; }

    public int Qty { get; set; }

    public decimal Subtotal { get; set; }

    public Tenant? Tenant { get; set; }

    public Transaction? Transaction { get; set; }

    public Product? Product { get; set; }
}
