using NeverfadePos.Api.BusinessModes;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class BusinessModeCatalogTests
{
    public static TheoryData<string, string[]> Presets =>
        new()
        {
            {
                BusinessTypes.GeneralRetail,
                [
                    TenantCapabilities.CorePos,
                    TenantCapabilities.Inventory,
                    TenantCapabilities.Customers,
                    TenantCapabilities.Reports,
                    TenantCapabilities.Attendance,
                    TenantCapabilities.FinanceWithdrawal
                ]
            },
            {
                BusinessTypes.FoodBeverage,
                [
                    TenantCapabilities.CorePos,
                    TenantCapabilities.Inventory,
                    TenantCapabilities.Customers,
                    TenantCapabilities.Reports,
                    TenantCapabilities.Attendance,
                    TenantCapabilities.FinanceWithdrawal,
                    TenantCapabilities.TableOrders,
                    TenantCapabilities.KitchenQueue
                ]
            },
            {
                BusinessTypes.Laundry,
                [
                    TenantCapabilities.CorePos,
                    TenantCapabilities.Inventory,
                    TenantCapabilities.Customers,
                    TenantCapabilities.Reports,
                    TenantCapabilities.Attendance,
                    TenantCapabilities.FinanceWithdrawal,
                    TenantCapabilities.WorkOrders
                ]
            },
            {
                BusinessTypes.SalonBarbershop,
                [
                    TenantCapabilities.CorePos,
                    TenantCapabilities.Inventory,
                    TenantCapabilities.Customers,
                    TenantCapabilities.Reports,
                    TenantCapabilities.Attendance,
                    TenantCapabilities.FinanceWithdrawal,
                    TenantCapabilities.Appointments
                ]
            }
        };

    [Theory]
    [MemberData(nameof(Presets))]
    public void Resolve_ReturnsExactDeterministicPreset(
        string businessType,
        string[] expected)
    {
        var actual = BusinessCapabilityPresets.Resolve(businessType);

        Assert.Equal(expected, actual);
        Assert.All(expected, capability =>
            Assert.True(BusinessCapabilityPresets.HasCapability(
                businessType,
                capability)));
    }

    [Fact]
    public void Resolve_RejectsUnknownBusinessType()
    {
        Assert.Throws<ArgumentException>(() =>
            BusinessCapabilityPresets.Resolve("hotel"));
    }

    [Fact]
    public void Resolve_DoesNotExposeMutablePresetStorage()
    {
        var first = BusinessCapabilityPresets.Resolve(
            BusinessTypes.FoodBeverage);
        var second = BusinessCapabilityPresets.Resolve(
            BusinessTypes.FoodBeverage);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" general_retail")]
    [InlineData("GENERAL_RETAIL")]
    [InlineData("hotel")]
    public void BusinessTypes_IsValid_IsStrict(string? businessType)
    {
        Assert.False(BusinessTypes.IsValid(businessType));
    }

    [Theory]
    [InlineData(BusinessTypes.GeneralRetail)]
    [InlineData(BusinessTypes.FoodBeverage)]
    [InlineData(BusinessTypes.Laundry)]
    [InlineData(BusinessTypes.SalonBarbershop)]
    public void BusinessTypes_IsValid_AcceptsSupportedTypes(string businessType)
    {
        Assert.True(BusinessTypes.IsValid(businessType));
    }
}
