namespace NeverfadePos.Api.DTOs.PlatformTenant;

public sealed class PlatformTenantDto
{
    public Guid Id { get; set; }

    public string NamaToko { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public TenantOwnerSummaryDto? Owner { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class TenantOwnerSummaryDto
{
    public Guid Id { get; set; }

    public string Nama { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public bool Active { get; set; }
}
