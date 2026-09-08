using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class RestaurantTable : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
    public bool Active { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public ICollection<RestaurantOrder> Orders { get; set; } =
        new List<RestaurantOrder>();
}
