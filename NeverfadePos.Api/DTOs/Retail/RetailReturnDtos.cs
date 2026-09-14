namespace NeverfadePos.Api.DTOs.Retail;

public sealed class CreateRetailReturnDto
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid TransactionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<CreateRetailReturnItemDto> Items { get; set; } = [];
}

public sealed class CreateRetailReturnItemDto
{
    public Guid TransactionItemId { get; set; }
    public int Quantity { get; set; }
    public bool Restock { get; set; } = true;
    public Guid? ReplacementVariantId { get; set; }
}

public sealed class RetailReturnDto
{
    public Guid Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid TransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<RetailReturnItemDto> Items { get; set; } = [];
}

public sealed class RetailReturnItemDto
{
    public Guid Id { get; set; }
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
}
