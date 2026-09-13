using NeverfadePos.Api.Services.Retail;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class RetailPricingRulesTests
{
    private static readonly Guid Wholesale = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Reseller = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Variant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Automatic_UsesHighestEligibleQuantityBreak()
    {
        var result = RetailPricingRules.ResolveAutomatic(
            100_000m,
            12m,
            null,
            [
                new(Wholesale, "Grosir", 10, null, 6m, 85_000m),
                new(Reseller, "Reseller", 20, null, 12m, 75_000m)
            ]);

        Assert.Equal(75_000m, result.UnitPrice);
        Assert.Equal(Reseller, result.PriceLevelId);
        Assert.Equal("Reseller", result.PriceLevelName);
    }

    [Fact]
    public void Automatic_FallsBackToBasePriceWhenNoBreakEligible()
    {
        var result = RetailPricingRules.ResolveAutomatic(
            100_000m,
            2m,
            null,
            [new(Wholesale, "Grosir", 10, null, 6m, 85_000m)]);

        Assert.Equal(100_000m, result.UnitPrice);
        Assert.Null(result.PriceLevelId);
    }

    [Fact]
    public void VariantSpecificPrice_OverridesProductLevelForSameLevel()
    {
        var result = RetailPricingRules.ResolveAutomatic(
            110_000m,
            6m,
            Variant,
            [
                new(Wholesale, "Grosir", 10, null, 6m, 85_000m),
                new(Wholesale, "Grosir", 10, Variant, 6m, 90_000m)
            ]);

        Assert.Equal(90_000m, result.UnitPrice);
        Assert.Equal(Wholesale, result.PriceLevelId);
    }

    [Fact]
    public void ManualConfiguredLevel_CanBeSelectedBelowAutomaticMinimum()
    {
        var ok = RetailPricingRules.TryResolveManual(
            100_000m,
            null,
            Wholesale,
            [new(Wholesale, "Grosir", 10, null, 6m, 85_000m)],
            out var result);

        Assert.True(ok);
        Assert.Equal(85_000m, result.UnitPrice);
        Assert.Equal(Wholesale, result.PriceLevelId);
    }
}
