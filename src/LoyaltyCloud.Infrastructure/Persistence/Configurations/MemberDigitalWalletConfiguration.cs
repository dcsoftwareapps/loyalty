using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class MemberDigitalWalletConfiguration : IEntityTypeConfiguration<MemberDigitalWallet>
{
    public void Configure(EntityTypeBuilder<MemberDigitalWallet> builder)
    {
        builder.ToTable("MemberDigitalWallets");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.TenantId).IsRequired();

        builder.Property(w => w.Provider)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.ExternalClassId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(w => w.ExternalObjectId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(w => w.LastSynchronizationError)
            .HasMaxLength(1000);

        builder.Property(w => w.MetadataJson)
            .HasMaxLength(4000);

        builder.Property(w => w.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(w => w.UpdatedAt).HasColumnType("datetime2(3)");
        builder.Property(w => w.LastSynchronizedAt).HasColumnType("datetime2(3)");
        builder.Property(w => w.LastSaveLinkCreatedAt).HasColumnType("datetime2(3)");
        builder.Property(w => w.RevokedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(w => new { w.TenantId, w.LoyaltyCardId, w.Provider }).IsUnique();
        builder.HasIndex(w => new { w.Provider, w.ExternalObjectId }).IsUnique();
        builder.HasIndex(w => new { w.TenantId, w.CustomerId, w.Provider });
        builder.HasIndex(w => new { w.TenantId, w.Provider, w.Status });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(w => w.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasForeignKey(w => w.LoyaltyCardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

