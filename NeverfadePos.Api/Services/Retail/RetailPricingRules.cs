namespace NeverfadePos.Api.Services.Retail;

public sealed record RetailPriceCandidate(
    Guid PriceLevelId,
    string PriceLevelName,
    int SortOrder,
    Guid? ProductVariantId,
    decimal MinQuantity,
    decimal UnitPrice);

public sealed record RetailPriceResolution(
    decimal BasePrice,
    decimal UnitPrice,
    Guid? PriceLevelId,
    string PriceLevelName,
    decimal? MinQuantity);

public static class RetailPricingRules
{
    public static RetailPriceResolution ResolveAutomatic(
        decimal basePrice,
        decimal quantity,
        Guid? variantId,
        IEnumerable<RetailPriceCandidate> candidates)
    {
        var eligible = EffectiveCandidates(variantId, candidates)
            .Where(x => x.MinQuantity <= quantity)
            .OrderByDescending(x => x.MinQuantity)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.PriceLevelName, StringComparer.Ordinal)
            .FirstOrDefault();

        return eligible is null
            ? new RetailPriceResolution(basePrice, basePrice, null, string.Empty, null)
            : new RetailPriceResolution(
                basePrice,
                eligible.UnitPrice,
                eligible.PriceLevelId,
                eligible.PriceLevelName,
                eligible.MinQuantity);
    }

    public static bool TryResolveManual(
        decimal basePrice,
        Guid? variantId,
        Guid priceLevelId,
        IEnumerable<RetailPriceCandidate> candidates,
        out RetailPriceResolution resolution)
    {
        var candidate = EffectiveCandidates(variantId, candidates)
            .FirstOrDefault(x => x.PriceLevelId == priceLevelId);

        if (candidate is null)
        {
            resolution = new RetailPriceResolution(basePrice, basePrice, null, string.Empty, null);
            return false;
        }

        resolution = new RetailPriceResolution(
            basePrice,
            candidate.UnitPrice,
            candidate.PriceLevelId,
            candidate.PriceLevelName,
            candidate.MinQuantity);
        return true;
    }

    private static IReadOnlyList<RetailPriceCandidate> EffectiveCandidates(
        Guid? variantId,
        IEnumerable<RetailPriceCandidate> candidates)
    {
        return candidates
            .Where(x => x.ProductVariantId is null || x.ProductVariantId == variantId)
            .GroupBy(x => x.PriceLevelId)
            .Select(group => group
                .OrderByDescending(x =>
                    variantId.HasValue &&
                    x.ProductVariantId == variantId)
                .First())
            .ToList();
    }
}
