using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyCardConfiguration : IEntityTypeConfiguration<LoyaltyCard>
{
    public void Configure(EntityTypeBuilder<LoyaltyCard> builder)
    {
        builder.ToTable("LoyaltyCards");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.SerialNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Level)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.AuthenticationToken)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.LevelAchievedAt).HasColumnType("datetime2(3)");
        builder.Property(c => c.LastActivityAt).HasColumnType("datetime2(3)");

        // Único: un serial nunca se repite.
        builder.HasIndex(c => c.SerialNumber).IsUnique();

        builder.HasAlternateKey(c => new { c.TenantId, c.SerialNumber });

        // 1:1 con Customer — un cliente tiene una tarjeta.
        builder.HasIndex(c => new { c.TenantId, c.CustomerId }).IsUnique();

        // FK explícita a Customer (sin nav property en el dominio).
        builder.HasOne<Customer>()
            .WithOne()
            .HasPrincipalKey<Customer>(c => new { c.TenantId, c.Id })
            .HasForeignKey<LoyaltyCard>(c => new { c.TenantId, c.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
