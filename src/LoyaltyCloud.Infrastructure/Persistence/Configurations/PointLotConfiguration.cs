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

        builder.HasIndex(l => l.SourcePointTransactionId)
            .IsUnique();

        builder.HasIndex(l => new { l.LoyaltyCardId, l.ExpiresAt, l.EarnedAt });
        builder.HasIndex(l => new { l.ExpiresAt, l.RemainingAmount });

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasForeignKey(l => l.LoyaltyCardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PointTransaction>()
            .WithOne()
            .HasForeignKey<PointLot>(l => l.SourcePointTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
