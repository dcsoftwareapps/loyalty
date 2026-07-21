using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class PointCampaignConfiguration : IEntityTypeConfiguration<PointCampaign>
{
    public void Configure(EntityTypeBuilder<PointCampaign> builder)
    {
        builder.ToTable("PointCampaigns");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(c => c.MinimumPurchaseAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.LevelEligibility)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.StartsAtUtc).HasColumnType("datetime2(3)");
        builder.Property(c => c.EndsAtUtc).HasColumnType("datetime2(3)");
        builder.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(c => new { c.TenantId, c.Id }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.IsActive });
        builder.HasIndex(c => new { c.TenantId, c.StartsAtUtc });
        builder.HasIndex(c => new { c.TenantId, c.EndsAtUtc });
        builder.HasIndex(c => new { c.TenantId, c.IsActive, c.StartsAtUtc, c.EndsAtUtc });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
