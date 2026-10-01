using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class EarnedStampRewardConfiguration : IEntityTypeConfiguration<EarnedStampReward>
{
    public void Configure(EntityTypeBuilder<EarnedStampReward> b)
    {
        b.ToTable("EarnedStampRewards"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.TenantId, x.SourceEarnTransactionId, x.RequiredStamps }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.StampCycleId, x.Status });
        b.HasOne<StampCycle>().WithMany().HasForeignKey(x => new { x.TenantId, x.StampCycleId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StampTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, x.SourceEarnTransactionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        // Definition ID is provenance, not a FK: its future removal must not remove an earned snapshot.
        foreach (var name in new[] { nameof(EarnedStampReward.StampCycleId), nameof(EarnedStampReward.SourceEarnTransactionId),
            nameof(EarnedStampReward.StampRewardDefinitionId), nameof(EarnedStampReward.Name),
            nameof(EarnedStampReward.Description), nameof(EarnedStampReward.RequiredStamps), nameof(EarnedStampReward.EarnedAt) })
            b.Property(name).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}

internal sealed class StampOperationReceiptConfiguration : IEntityTypeConfiguration<StampOperationReceipt>
{
    public void Configure(EntityTypeBuilder<StampOperationReceipt> b)
    {
        b.ToTable("StampOperationReceipts"); b.HasKey(x => x.Id);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.Operation).HasMaxLength(20).IsRequired();
        b.Property(x => x.SerialNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.ResponseJson).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        b.HasOne<LoyaltyCard>().WithMany().HasForeignKey(x => new { x.TenantId, x.SerialNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.SerialNumber }).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in b.Metadata.GetProperties().Where(p => !p.IsKey()))
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
