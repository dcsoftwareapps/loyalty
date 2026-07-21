using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.TimeZoneId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasIndex(t => t.IsActive);

        builder.HasOne(t => t.Branding)
            .WithOne(b => b.Tenant)
            .HasForeignKey<TenantBranding>(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Subscription)
            .WithOne(s => s.Tenant)
            .HasForeignKey<TenantSubscription>(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.AdminUsers)
            .WithOne(u => u.Tenant)
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(t => t.AdminUsers)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
