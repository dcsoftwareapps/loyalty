using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class TenantLoyaltyLevelConfiguration : IEntityTypeConfiguration<TenantLoyaltyLevel>
{
    public void Configure(EntityTypeBuilder<TenantLoyaltyLevel> builder)
    {
        builder.ToTable("TenantLoyaltyLevels");

        builder.HasKey(level => level.Id);

        builder.Property(level => level.Name)
            .HasMaxLength(TenantLoyaltyLevel.NameMaxLength)
            .IsRequired();

        builder.Property(level => level.NormalizedName)
            .HasMaxLength(TenantLoyaltyLevel.NameMaxLength)
            .IsRequired();

        builder.Property(level => level.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(level => level.UpdatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(level => new { level.TenantId, level.NormalizedName }).IsUnique();
        builder.HasIndex(level => new { level.TenantId, level.SortOrder }).IsUnique();
        builder.HasIndex(level => new { level.TenantId, level.Threshold }).IsUnique();
        builder.HasIndex(level => new { level.TenantId, level.IsActive, level.SortOrder });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(level => level.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
