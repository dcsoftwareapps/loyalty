using KBeauty.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KBeauty.Loyalty.Infrastructure.Persistence.Configurations;

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

        builder.Property(r => r.ConfirmedBy).HasMaxLength(100);
        builder.Property(r => r.Notes).HasMaxLength(500);

        builder.Property(r => r.RedeemedAt).HasColumnType("datetime2(3)");
        builder.Property(r => r.ConfirmedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(r => r.LoyaltyCardId);
        builder.HasIndex(r => r.Status); // para listar Pending rápido en el panel admin

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasForeignKey(r => r.LoyaltyCardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RewardCatalogItem>()
            .WithMany()
            .HasForeignKey(r => r.RewardCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
