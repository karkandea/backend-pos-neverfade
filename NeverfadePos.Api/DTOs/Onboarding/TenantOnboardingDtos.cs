namespace NeverfadePos.Api.DTOs.Onboarding;

public sealed class TenantOnboardingDto
{
    public Guid TenantId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public int CompletedRequired { get; set; }
    public int TotalRequired { get; set; }
    /// <summary>Only setup checklist state; NOT a production release certificate.</summary>
    public bool RequiredStepsComplete { get; set; }
    public List<TenantOnboardingStepDto> Steps { get; set; } = [];
}

public sealed class TenantOnboardingStepDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionPath { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool Complete { get; set; }
}
