using NeverfadePos.Api.BusinessModes;

namespace NeverfadePos.Api.DemoMode;

public sealed record DemoProfile(
    string Key,
    Guid TenantId,
    string TenantSlug,
    string Username,
    string StoreName,
    string BusinessType);

public static class DemoModeDefaults
{
    public static readonly DemoProfile Fashion = new(
        BusinessTypes.FashionRetail,
        Guid.Parse("7d8f2e35-63f2-4aa5-8e32-f3bbcc0a7d01"),
        "neverfade-demo-fashion",
        "demo-fashion",
        "NeverFade Fashion Demo",
        BusinessTypes.FashionRetail);

    public static readonly DemoProfile GeneralRetail = new(
        BusinessTypes.GeneralRetail,
        Guid.Parse("7d8f2e35-63f2-4aa5-8e32-f3bbcc0a7d02"),
        "neverfade-demo-retail",
        "demo-retail",
        "NeverFade Retail Demo",
        BusinessTypes.GeneralRetail);

    public static readonly DemoProfile FoodBeverage = new(
        BusinessTypes.FoodBeverage,
        Guid.Parse("7d8f2e35-63f2-4aa5-8e32-f3bbcc0a7d03"),
        "neverfade-demo-fnb",
        "demo-fnb",
        "NeverFade Cafe Demo",
        BusinessTypes.FoodBeverage);

    public static readonly DemoProfile Laundry = new(
        BusinessTypes.Laundry,
        Guid.Parse("7d8f2e35-63f2-4aa5-8e32-f3bbcc0a7d04"),
        "neverfade-demo-laundry",
        "demo-laundry",
        "NeverFade Laundry Demo",
        BusinessTypes.Laundry);

    public static readonly DemoProfile SalonBarbershop = new(
        BusinessTypes.SalonBarbershop,
        Guid.Parse("7d8f2e35-63f2-4aa5-8e32-f3bbcc0a7d05"),
        "neverfade-demo-salon",
        "demo-salon",
        "NeverFade Salon Demo",
        BusinessTypes.SalonBarbershop);

    public static readonly IReadOnlyList<DemoProfile> Profiles =
    [
        GeneralRetail,
        Fashion,
        FoodBeverage,
        Laundry,
        SalonBarbershop
    ];

    private static readonly IReadOnlyDictionary<string, DemoProfile> ByKey =
        Profiles.ToDictionary(x => x.Key, StringComparer.Ordinal);

    private static readonly IReadOnlySet<Guid> TenantIds =
        Profiles.Select(x => x.TenantId).ToHashSet();

    public static DemoProfile DefaultProfile => Fashion;

    public static bool TryResolve(
        string? businessType,
        out DemoProfile profile)
    {
        if (string.IsNullOrWhiteSpace(businessType))
        {
            profile = DefaultProfile;
            return true;
        }

        return ByKey.TryGetValue(businessType, out profile!);
    }

    public static bool IsDemoTenant(Guid tenantId) =>
        TenantIds.Contains(tenantId);

    public static bool IsExactDemoTenantSet(IEnumerable<Guid> tenantIds)
    {
        var ids = tenantIds.ToHashSet();

        return ids.Count == TenantIds.Count &&
               ids.SetEquals(TenantIds);
    }
}

public sealed class DemoModeOptions
{
    public bool Enabled { get; set; }
}
