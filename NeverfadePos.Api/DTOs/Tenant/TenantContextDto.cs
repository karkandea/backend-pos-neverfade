namespace NeverfadePos.Api.DTOs.Tenant;

public sealed class TenantContextDto
{
    public Guid TenantId { get; set; }

    public string NamaToko { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; set; } = [];

    public string Role { get; set; } = string.Empty;

    public IReadOnlyList<string> EffectivePermissions { get; set; } = [];

    public string TenantStatus { get; set; } = string.Empty;

    public IReadOnlyList<Guid> AssignedOutletIds { get; set; } = [];
}
