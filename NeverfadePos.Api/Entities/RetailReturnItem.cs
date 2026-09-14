using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class RetailReturnItem : BaseEntity
{
    public Guid RetailReturnId { get; set; }
    public Guid TransactionItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? OriginalVariantId { get; set; }
    public string OriginalVariantSku { get; set; } = string.Empty;
    public string OriginalVariantLabel { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal OriginalUnitPrice { get; set; }
    public decimal RefundAmount { get; set; }
    public bool Restock { get; set; }
    public Guid? ReplacementVariantId { get; set; }
    public string ReplacementVariantSku { get; set; } = string.Empty;
    public string ReplacementVariantLabel { get; set; } = string.Empty;

    public RetailReturn? RetailReturn { get; set; }
    public TransactionItem? TransactionItem { get; set; }
    public Product? Product { get; set; }
}
