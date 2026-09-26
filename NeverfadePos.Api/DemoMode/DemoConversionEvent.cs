using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NeverfadePos.Api.DemoMode;

/// <summary>Anonymous, allowlisted event only. No IP, device ID, name, or contact data.</summary>
public sealed class DemoConversionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string? Mode { get; set; }
    public string? Step { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DemoConversionEventConfiguration : IEntityTypeConfiguration<DemoConversionEvent>
{
    public void Configure(EntityTypeBuilder<DemoConversionEvent> builder)
    {
        builder.ToTable("demo_conversion_events");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CreatedAt, x.EventName });
        builder.HasIndex(x => new { x.SessionId, x.CreatedAt });
        builder.Property(x => x.BusinessType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EventName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(12);
        builder.Property(x => x.Step).HasMaxLength(48);
    }
}
