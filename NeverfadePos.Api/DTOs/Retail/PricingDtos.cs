using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Retail;

public sealed class PriceLevelDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Active { get; set; }
}

public class CreatePriceLevelDto
{
    [Required, MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class UpdatePriceLevelDto : CreatePriceLevelDto
{
    public bool Active { get; set; } = true;
}

public sealed class ProductPriceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public Guid PriceLevelId { get; set; }
    public string PriceLevelCode { get; set; } = string.Empty;
    public string PriceLevelName { get; set; } = string.Empty;
    public int PriceLevelSortOrder { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CreateProductPriceDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public Guid PriceLevelId { get; set; }
    [Range(0.001, double.MaxValue)] public decimal MinQuantity { get; set; } = 1m;
    [Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
}

public sealed class UpdateProductPriceDto : CreateProductPriceDto;
