using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class OpsAlert : BaseEntity
{
    // Unique per tenant + failure source: replays never create another alert.
    public string SourceKind { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string State { get; set; } = "open";
}
