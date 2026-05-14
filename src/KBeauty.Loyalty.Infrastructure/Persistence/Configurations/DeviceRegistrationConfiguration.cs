using KBeauty.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KBeauty.Loyalty.Infrastructure.Persistence.Configurations;

internal sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("DeviceRegistrations");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceLibraryIdentifier)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.PassTypeIdentifier)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.SerialNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.PushToken)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.CreatedAt).HasColumnType("datetime2(3)");

        // Tripleta única requerida por el flujo de Apple Wallet.
        builder.HasIndex(d => new { d.DeviceLibraryIdentifier, d.PassTypeIdentifier, d.SerialNumber })
            .IsUnique();

        // Búsqueda por serial al enviar pushes a todos los dispositivos de una clienta.
        builder.HasIndex(d => d.SerialNumber);
    }
}
