using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.DemoMode;

public sealed class DemoVisitorSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CookieHash { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

public sealed class DemoVisitorSessionConfiguration : IEntityTypeConfiguration<DemoVisitorSession>
{
    public void Configure(EntityTypeBuilder<DemoVisitorSession> builder)
    {
        builder.ToTable("demo_visitor_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CookieHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.BusinessType).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.CookieHash, x.BusinessType }).IsUnique();
        builder.HasIndex(x => x.TenantId).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
