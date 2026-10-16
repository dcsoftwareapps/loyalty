using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
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

        builder.Property(u => u.Email)
            .HasMaxLength(254);

        builder.Property(u => u.NormalizedEmail)
            .HasMaxLength(254);

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(TenantUserRole.Admin)
            .IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(u => u.LastLoginAt).HasColumnType("datetime2(3)");

        builder.HasIndex(u => new { u.TenantId, u.NormalizedUsername }).IsUnique();
        builder.HasIndex(u => new { u.TenantId, u.NormalizedEmail })
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");
        builder.HasIndex(u => u.IsActive);
    }
}
