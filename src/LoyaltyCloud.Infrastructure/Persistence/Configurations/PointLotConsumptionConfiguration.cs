using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class PointLotConsumptionConfiguration : IEntityTypeConfiguration<PointLotConsumption>
{
    public void Configure(EntityTypeBuilder<PointLotConsumption> builder)
    {
        builder.ToTable("PointLotConsumptions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(c => c.ReversedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(c => new { c.TenantId, c.PointLotId });
        builder.HasIndex(c => new { c.TenantId, c.ConsumingPointTransactionId });
        builder.HasIndex(c => new { c.TenantId, c.RedemptionId })
            .HasFilter("[RedemptionId] IS NOT NULL");
        builder.HasIndex(c => new { c.TenantId, c.CreatedAt });

        builder.HasOne<PointLot>()
            .WithMany()
            .HasPrincipalKey(l => new { l.TenantId, l.Id })
            .HasForeignKey(c => new { c.TenantId, c.PointLotId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PointTransaction>()
            .WithMany()
            .HasPrincipalKey(t => new { t.TenantId, t.Id })
            .HasForeignKey(c => new { c.TenantId, c.ConsumingPointTransactionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Redemption>()
            .WithMany()
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .HasForeignKey(c => new { c.TenantId, c.RedemptionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
