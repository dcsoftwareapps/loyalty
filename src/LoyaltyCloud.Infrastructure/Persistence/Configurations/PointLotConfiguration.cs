using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class PointLotConfiguration : IEntityTypeConfiguration<PointLot>
{
    public void Configure(EntityTypeBuilder<PointLot> builder)
    {
        builder.ToTable("PointLots");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.EarnedAt).HasColumnType("datetime2(3)");
        builder.Property(l => l.ExpiresAt).HasColumnType("datetime2(3)");
        builder.Property(l => l.CreatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(l => new { l.TenantId, l.SourcePointTransactionId }).IsUnique();
        builder.HasIndex(l => new { l.TenantId, l.LoyaltyCardId, l.ExpiresAt, l.EarnedAt });
        builder.HasIndex(l => new { l.TenantId, l.ExpiresAt, l.RemainingAmount });

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasForeignKey(l => new { l.TenantId, l.LoyaltyCardId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PointTransaction>()
            .WithOne()
            .HasPrincipalKey<PointTransaction>(t => new { t.TenantId, t.Id })
            .HasForeignKey<PointLot>(l => new { l.TenantId, l.SourcePointTransactionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
