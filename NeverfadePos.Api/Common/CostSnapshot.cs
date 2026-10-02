using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Common;

public static class CostSnapshot
{
    // No historic revaluation from mutable product catalog; missing/zero cost
    // is deliberately marked unknown rather than assumed to be free stock.
    public static decimal? Resolve(Product product, ProductVariant? variant)
    {
        var cost = variant?.HargaModal is > 0m
            ? variant.HargaModal.Value
            : product.HargaModal;
        return cost > 0m ? Math.Round(cost, 2, MidpointRounding.AwayFromZero) : null;
    }
}
