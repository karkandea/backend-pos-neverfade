using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Sales;

public sealed class CreateSaleQuoteRequestDto
{
    public Guid OutletId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? DiscountCode { get; set; }
    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }
    [MinLength(1)]
    [MaxLength(100)]
    public List<SaleQuoteLineRequestDto> Lines { get; set; } = [];
}

public sealed class SaleQuoteLineRequestDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? PriceLevelId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class SaleQuoteLineDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? PriceLevelId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string PriceLevelName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public sealed class SaleQuoteDto
{
    public Guid QuoteId { get; set; }
    public Guid QuoteVersion { get; set; }
    public Guid OutletId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = "quoted";
    public bool StockReserved { get; set; }
    public List<SaleQuoteLineDto> Lines { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal Tax { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "IDR";
    public List<string> Warnings { get; set; } = [];
}

public sealed class SaleQuoteResponseDto
{
    public SaleQuoteDto Data { get; set; } = new();
    public SaleQuoteMetaDto Meta { get; set; } = new();
}

public sealed class SaleQuoteMetaDto
{
    public string CorrelationId { get; set; } = string.Empty;
}
