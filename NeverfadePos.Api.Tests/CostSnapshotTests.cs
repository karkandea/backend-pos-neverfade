using NeverfadePos.Api.Common;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class CostSnapshotTests
{
    [Fact]
    public void Snapshot_UsesVariantFirst_AndUnknownCostsRemainNull()
    {
        var product = new Product { HargaModal = 50m };
        var variant = new ProductVariant { HargaModal = 38m };
        Assert.Equal(38m, CostSnapshot.Resolve(product, variant));
        variant.HargaModal = null;
        Assert.Equal(50m, CostSnapshot.Resolve(product, variant));
        product.HargaModal = 0m;
        Assert.Null(CostSnapshot.Resolve(product, variant));
    }
}
