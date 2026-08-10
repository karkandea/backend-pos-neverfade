namespace NeverfadePos.Api.Auth;

public static class PlatformAuthConstants
{
    public const string AuthenticationScheme =
        "PlatformBearer";

    public const string AuthorizationPolicy =
        "PlatformSuperAdmin";

    public const string ScopeClaim = "scope";
    public const string PlatformScope = "platform";
    public const string SuperAdminRole = "superadmin";
}
