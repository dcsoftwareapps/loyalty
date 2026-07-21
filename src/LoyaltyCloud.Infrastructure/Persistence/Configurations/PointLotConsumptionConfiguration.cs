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

        builder.HasIndex(c => c.PointLotId);
        builder.HasIndex(c => c.ConsumingPointTransactionId);
        builder.HasIndex(c => c.RedemptionId);

        builder.HasOne<PointLot>()
            .WithMany()
            .HasForeignKey(c => c.PointLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PointTransaction>()
            .WithMany()
            .HasForeignKey(c => c.ConsumingPointTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Redemption>()
            .WithMany()
            .HasForeignKey(c => c.RedemptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
