using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class PriceLevel : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
}
