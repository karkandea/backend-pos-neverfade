using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Configurations;

public sealed class WithdrawalBankAccountConfiguration
    : IEntityTypeConfiguration<WithdrawalBankAccount>
{
    public void Configure(EntityTypeBuilder<WithdrawalBankAccount> builder)
    {
        builder.ToTable(
            "withdrawal_bank_accounts",
            table => table.HasCheckConstraint(
                "CK_withdrawal_bank_accounts_VerificationStatus",
                "\"VerificationStatus\" IN ('pending', 'verified', 'rejected')"));

        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId).IsUnique();
        builder.HasIndex(x => x.VerificationStatus);

        builder.Property(x => x.BankName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.VerificationStatus).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.WithdrawalBankAccounts)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VerifiedByPlatformUser)
            .WithMany()
            .HasForeignKey(x => x.VerifiedByPlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
