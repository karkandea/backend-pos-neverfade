namespace NeverfadePos.Api.DTOs.Job;

public sealed class JobDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public Guid? ResultReference { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
