namespace NeverfadePos.Api.BusinessModes;

public static class BusinessTypes
{
    public const string GeneralRetail = "general_retail";
    public const string FoodBeverage = "food_beverage";
    public const string Laundry = "laundry";
    public const string SalonBarbershop = "salon_barbershop";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GeneralRetail,
            FoodBeverage,
            Laundry,
            SalonBarbershop
        };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value);
}

public static class TenantCapabilities
{
    public const string CorePos = "core_pos";
    public const string Inventory = "inventory";
    public const string Customers = "customers";
    public const string Reports = "reports";
    public const string Attendance = "attendance";
    public const string FinanceWithdrawal = "finance_withdrawal";
    public const string TableOrders = "table_orders";
    public const string KitchenQueue = "kitchen_queue";
    public const string WorkOrders = "work_orders";
    public const string Appointments = "appointments";
}

public static class BusinessCapabilityPresets
{
    private static readonly string[] Common =
    [
        TenantCapabilities.CorePos,
        TenantCapabilities.Inventory,
        TenantCapabilities.Customers,
        TenantCapabilities.Reports,
        TenantCapabilities.Attendance,
        TenantCapabilities.FinanceWithdrawal
    ];

    private static readonly IReadOnlyDictionary<string, string[]> Presets =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [BusinessTypes.GeneralRetail] = Common,
            [BusinessTypes.FoodBeverage] =
            [
                ..Common,
                TenantCapabilities.TableOrders,
                TenantCapabilities.KitchenQueue
            ],
            [BusinessTypes.Laundry] =
            [
                ..Common,
                TenantCapabilities.WorkOrders
            ],
            [BusinessTypes.SalonBarbershop] =
            [
                ..Common,
                TenantCapabilities.Appointments
            ]
        };

    public static IReadOnlyList<string> Resolve(string businessType)
    {
        if (!Presets.TryGetValue(businessType, out var capabilities))
        {
            throw new ArgumentException(
                "Tipe bisnis tidak valid.",
                nameof(businessType));
        }

        return Array.AsReadOnly((string[])capabilities.Clone());
    }

    public static bool HasCapability(
        string businessType,
        string capability) =>
        Resolve(businessType).Contains(
            capability,
            StringComparer.Ordinal);
}
