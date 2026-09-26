using NeverfadePos.Api.BusinessModes;

namespace NeverfadePos.Api.Auth;

/// <summary>Server-side role defaults. Business capabilities are an independent gate.</summary>
public static class RolePermissionCatalog
{
    private static readonly string[] Cashier =
    [
        "pos.sell", "products.read", "customers.search", "customers.create",
        "transactions.read", "attendance.self", "outlets.read",
        "restaurant.tables.read", "restaurant.orders.operate", "restaurant.kitchen.operate",
        "laundry.orders.operate"
    ];

    private static readonly string[] Manager =
    [
        ..Cashier, "products.manage", "inventory.manage", "customers.manage",
        "reports.read", "attendance.manage", "users.manage", "outlets.manage",
        "settings.manage", "restaurant.tables.manage", "retail.manage",
        "laundry.orders.manage"
    ];

    public static IReadOnlyList<string> Resolve(string role, IReadOnlyList<string> capabilities)
    {
        var defaults = role switch
        {
            "owner" => [..Manager, "finance.read", "finance.manage", "tenant.manage"],
            "admin" => Manager,
            "kasir" => Cashier,
            "dapur" => ["outlets.read", "restaurant.kitchen.operate"],
            _ => Array.Empty<string>()
        };

        return defaults.Where(permission =>
            RequiredCapability(permission) is not { } capability ||
            capabilities.Contains(capability, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? RequiredCapability(string permission) => permission switch
    {
        var value when value.StartsWith("restaurant.tables", StringComparison.Ordinal) => "table_orders",
        var value when value.StartsWith("restaurant.orders", StringComparison.Ordinal) => "table_orders",
        var value when value.StartsWith("restaurant.kitchen", StringComparison.Ordinal) => "kitchen_queue",
        var value when value.StartsWith("laundry.", StringComparison.Ordinal) => "work_orders",
        var value when value.StartsWith("retail.", StringComparison.Ordinal) => "product_variants",
        var value when value.StartsWith("finance.", StringComparison.Ordinal) => "finance_withdrawal",
        var value when value.StartsWith("reports.", StringComparison.Ordinal) => "reports",
        var value when value.StartsWith("inventory.", StringComparison.Ordinal) => "inventory",
        var value when value.StartsWith("customers.", StringComparison.Ordinal) => "customers",
        var value when value.StartsWith("attendance.", StringComparison.Ordinal) => "attendance",
        _ => null
    };
}
