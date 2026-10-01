using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class StampProgramVersionConfiguration : IEntityTypeConfiguration<StampProgramVersion>
{
    public void Configure(EntityTypeBuilder<StampProgramVersion> b)
    {
        b.ToTable("StampProgramVersions", t => t.HasCheckConstraint("CK_StampProgramVersion_Rules", "[Version] > 0 AND [MaxStamps] > 0 AND [WelcomeStamps] >= 0 AND [WelcomeStamps] < [MaxStamps]"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.HasIndex(x => new { x.TenantId, x.Version }).IsUnique();
        b.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsActive] = 1");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.ReversalPolicy).HasConversion<string>().HasMaxLength(30);
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TenantAdminUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        foreach (var name in new[] { nameof(StampProgramVersion.Version), nameof(StampProgramVersion.MaxStamps),
            nameof(StampProgramVersion.WelcomeStamps), nameof(StampProgramVersion.ReversalPolicy),
            nameof(StampProgramVersion.CreatedAt), nameof(StampProgramVersion.CreatedByUserId) })
            b.Property(name).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}

internal sealed class StampRewardDefinitionConfiguration : IEntityTypeConfiguration<StampRewardDefinition>
{
    public void Configure(EntityTypeBuilder<StampRewardDefinition> b)
    {
        b.ToTable("StampRewardDefinitions", t => t.HasCheckConstraint("CK_StampRewardDefinition_Threshold", "[RequiredStamps] > 0"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TenantId, x.StampProgramVersionId, x.RequiredStamps }).IsUnique();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasOne<StampProgramVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.StampProgramVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        foreach (var name in new[] { nameof(StampRewardDefinition.RequiredStamps), nameof(StampRewardDefinition.Name),
            nameof(StampRewardDefinition.Description), nameof(StampRewardDefinition.StampProgramVersionId) })
            b.Property(name).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}

internal sealed class StampCycleConfiguration : IEntityTypeConfiguration<StampCycle>
{
    public void Configure(EntityTypeBuilder<StampCycle> b)
    {
        b.ToTable("StampCycles", t => t.HasCheckConstraint("CK_StampCycle_Progress", "[Sequence] > 0 AND [CurrentStamps] >= 0"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.LoyaltyCardSerialNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.TenantId, x.LoyaltyCardSerialNumber, x.Sequence }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.LoyaltyCardSerialNumber }).IsUnique().HasFilter("[CompletedAt] IS NULL");
        b.HasOne<LoyaltyCard>().WithMany().HasForeignKey(x => new { x.TenantId, x.LoyaltyCardSerialNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.SerialNumber }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StampProgramVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.StampProgramVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StampTransactionConfiguration : IEntityTypeConfiguration<StampTransaction>
{
    public void Configure(EntityTypeBuilder<StampTransaction> b)
    {
        b.ToTable("StampTransactions", t => t.HasCheckConstraint("CK_StampTransaction_Delta", "[Delta] <> 0"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.HasIndex(x => new { x.TenantId, x.StampCycleId, x.Sequence }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.ReversesTransactionId }).IsUnique().HasFilter("[ReversesTransactionId] IS NOT NULL");
        b.HasIndex(x => new { x.TenantId, x.StampCycleId, x.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.HasOne<StampCycle>().WithMany().HasForeignKey(x => new { x.TenantId, x.StampCycleId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StampTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, x.ReversesTransactionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in b.Metadata.GetProperties().Where(p => !p.IsPrimaryKey() && !p.IsKey()))
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
