using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class LaundryWorkOrderItem : BaseEntity
{
    public Guid LaundryWorkOrderId { get; set; }
    public Guid ProductId { get; set; }

    public string Nama { get; set; } = string.Empty;
    public string ProductType { get; set; } = ProductTypes.Goods;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int QuantityPrecision { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public Tenant? Tenant { get; set; }
    public LaundryWorkOrder? LaundryWorkOrder { get; set; }
    public Product? Product { get; set; }
}
