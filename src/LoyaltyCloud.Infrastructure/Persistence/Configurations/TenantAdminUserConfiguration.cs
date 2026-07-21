using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class TenantAdminUserConfiguration : IEntityTypeConfiguration<TenantAdminUser>
{
    public void Configure(EntityTypeBuilder<TenantAdminUser> builder)
    {
        builder.ToTable("TenantAdminUsers");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.NormalizedUsername)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(u => u.LastLoginAt).HasColumnType("datetime2(3)");

        builder.HasIndex(u => new { u.TenantId, u.NormalizedUsername }).IsUnique();
        builder.HasIndex(u => u.IsActive);
    }
}
