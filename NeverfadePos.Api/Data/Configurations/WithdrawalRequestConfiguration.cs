using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class WithdrawalRequestConfiguration
    : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
    {
        builder.ToTable(
            "withdrawal_requests",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_withdrawal_requests_Amount",
                    "\"Amount\" > 0");
                table.HasCheckConstraint(
                    "CK_withdrawal_requests_Status",
                    "\"Status\" IN ('requested', 'processing', 'paid', 'rejected', 'cancelled')");
            });

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => x.RequestedByUserId);
        builder.HasIndex(x => x.ProcessedByPlatformUserId);

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DestinationBankName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DestinationAccountLast4).HasMaxLength(4).IsRequired();
        builder.Property(x => x.DestinationAccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TransferReference).HasMaxLength(200);
        builder.Property(x => x.EvidenceMetadata).HasMaxLength(2000);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.WithdrawalRequests)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany(x => x.WithdrawalRequests)
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProcessedByPlatformUser)
            .WithMany(x => x.ProcessedWithdrawalRequests)
            .HasForeignKey(x => x.ProcessedByPlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
