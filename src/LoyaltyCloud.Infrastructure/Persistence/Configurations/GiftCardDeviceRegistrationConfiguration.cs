using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class GiftCardDeviceRegistrationConfiguration : IEntityTypeConfiguration<GiftCardDeviceRegistration>
{
    public void Configure(EntityTypeBuilder<GiftCardDeviceRegistration> builder)
    {
        builder.ToTable("GiftCardDeviceRegistrations"); builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceLibraryIdentifier).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PassTypeIdentifier).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SerialNumber).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PushToken).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)"); builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(x => new { x.DeviceLibraryIdentifier, x.PassTypeIdentifier, x.SerialNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SerialNumber }); builder.HasIndex(x => new { x.TenantId, x.DeviceLibraryIdentifier });
        builder.HasOne<GiftCard>().WithMany().HasForeignKey(x => new { x.TenantId, x.GiftCardId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
