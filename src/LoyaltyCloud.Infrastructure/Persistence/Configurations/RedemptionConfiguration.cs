using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class RedemptionConfiguration : IEntityTypeConfiguration<Redemption>
{
    public void Configure(EntityTypeBuilder<Redemption> builder)
    {
        builder.ToTable("Redemptions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.MonetaryAmount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.MonetaryCurrency).HasMaxLength(3);
        builder.Property(r => r.MonetaryPointsPerPesoUnit).HasColumnType("decimal(18,4)");

        builder.Property(r => r.ConfirmedBy).HasMaxLength(100);
        builder.Property(r => r.Notes).HasMaxLength(500);

        builder.Property(r => r.RedeemedAt).HasColumnType("datetime2(3)");
        builder.Property(r => r.ConfirmedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(r => new { r.TenantId, r.LoyaltyCardId, r.RedeemedAt });
        builder.HasIndex(r => new { r.TenantId, r.Status, r.RedeemedAt });
        builder.HasIndex(r => new { r.TenantId, r.RewardCatalogItemId })
            .HasFilter("[RewardCatalogItemId] IS NOT NULL");
        builder.HasIndex(r => new { r.TenantId, r.Type, r.RedeemedAt });

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasForeignKey(r => new { r.TenantId, r.LoyaltyCardId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RewardCatalogItem>()
            .WithMany()
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .HasForeignKey(r => new { r.TenantId, r.RewardCatalogItemId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
