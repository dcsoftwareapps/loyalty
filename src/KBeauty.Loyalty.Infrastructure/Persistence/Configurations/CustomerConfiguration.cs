using KBeauty.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KBeauty.Loyalty.Infrastructure.Persistence.Configurations;

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

        builder.Property(c => c.DateOfBirth)
            .HasColumnType("date"); // sin componente hora — solo fecha

        builder.Property(c => c.CreatedAt)
            .HasColumnType("datetime2(3)");

        // Email único — case-insensitive a nivel SQL (collation default de SQL Server).
        builder.HasIndex(c => c.Email).IsUnique();

        // Referido: FK opcional al referidor (sin nav property, solo el id).
        builder.HasIndex(c => c.ReferredBy);
    }
}
