using KBeauty.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KBeauty.Loyalty.Infrastructure.Persistence.Configurations;

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

        builder.Property(t => t.CreatedBy)
            .HasMaxLength(100);

        // Enums como string en DB — más legible en queries ad-hoc y robusto ante reordering.
        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.BonusType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");

        // Índice para el historial de una tarjeta ordenado por fecha desc.
        builder.HasIndex(t => new { t.LoyaltyCardId, t.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasForeignKey(t => t.LoyaltyCardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
