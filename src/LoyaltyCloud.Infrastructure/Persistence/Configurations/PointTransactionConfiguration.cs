using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.ToTable("PointTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.PurchaseAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(t => t.AppliedMultiplier)
            .HasColumnType("decimal(5,2)");

        builder.Property(t => t.CreatedBy)
            .HasMaxLength(100);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.BonusType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(t => new { t.TenantId, t.LoyaltyCardId, t.CreatedAt })
            .IsDescending(false, false, true);
        builder.HasIndex(t => new { t.TenantId, t.Type, t.CreatedAt });
        builder.HasIndex(t => new { t.TenantId, t.CampaignId })
            .HasFilter("[CampaignId] IS NOT NULL");

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasForeignKey(t => new { t.TenantId, t.LoyaltyCardId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Campaign)
            .WithMany()
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasForeignKey(t => new { t.TenantId, t.CampaignId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
