namespace NeverfadePos.Api.DemoMode;

public static class DemoModeDefaults
{
    public static readonly Guid TenantId =
        Guid.Parse("7d8f2e35-63f2-4aa5-8e32-f3bbcc0a7d01");

    public const string TenantSlug = "neverfade-demo-retail";
    public const string Username = "demo";
}

public sealed class DemoModeOptions
{
    public bool Enabled { get; set; }
}
