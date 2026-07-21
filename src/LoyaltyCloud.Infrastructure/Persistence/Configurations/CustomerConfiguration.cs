using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FullName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Phone)
            .HasMaxLength(30);

        builder.Property(c => c.NormalizedPhone)
            .HasMaxLength(30);

        builder.Property(c => c.DateOfBirth)
            .HasColumnType("date"); // sin componente hora — solo fecha

        builder.Property(c => c.CreatedAt)
            .HasColumnType("datetime2(3)");

        // Email único — case-insensitive a nivel SQL (collation default de SQL Server).
        builder.HasIndex(c => new { c.TenantId, c.Email })
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL AND [Email] <> ''");

        builder.HasIndex(c => new { c.TenantId, c.NormalizedPhone })
            .IsUnique()
            .HasFilter("[NormalizedPhone] IS NOT NULL AND [NormalizedPhone] <> ''");

        builder.HasIndex(c => new { c.TenantId, c.IsActive });

        // Referido: FK opcional al referidor (sin nav property, solo el id).
        builder.HasIndex(c => new { c.TenantId, c.ReferredBy });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
