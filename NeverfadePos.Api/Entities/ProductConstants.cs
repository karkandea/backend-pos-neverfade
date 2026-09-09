namespace NeverfadePos.Api.Entities;

public static class ProductTypes
{
    public const string Goods = "goods";
    public const string Service = "service";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Goods,
            Service
        };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        All.Contains(value);
}
