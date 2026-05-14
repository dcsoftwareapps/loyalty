using KBeauty.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KBeauty.Loyalty.Infrastructure.Persistence.Configurations;

internal sealed class RewardCatalogItemConfiguration : IEntityTypeConfiguration<RewardCatalogItem>
{
    public void Configure(EntityTypeBuilder<RewardCatalogItem> builder)
    {
        builder.ToTable("RewardCatalogItems");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.MinLevel)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ValidFrom).HasColumnType("datetime2(3)");
        builder.Property(r => r.ValidTo).HasColumnType("datetime2(3)");

        // Solo un "producto del mes" activo a la vez — filtrado en el repo, no en índice.
        builder.HasIndex(r => r.IsMonthlyProduct);
        builder.HasIndex(r => r.IsActive);
    }
}
