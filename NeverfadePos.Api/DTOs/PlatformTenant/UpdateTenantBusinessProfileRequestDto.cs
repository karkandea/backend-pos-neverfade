using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeverfadePos.Api.DTOs.PlatformTenant;

public sealed class UpdateTenantBusinessProfileRequestDto
{
    public string BusinessType { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
