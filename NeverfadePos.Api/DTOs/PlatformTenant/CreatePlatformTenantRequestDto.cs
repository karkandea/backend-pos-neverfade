using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeverfadePos.Api.DTOs.PlatformTenant;

public sealed class CreatePlatformTenantRequestDto
{
    public string NamaToko { get; set; } = string.Empty;

    public CreatePlatformTenantOwnerRequestDto? Owner { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class CreatePlatformTenantOwnerRequestDto
{
    public string Nama { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
