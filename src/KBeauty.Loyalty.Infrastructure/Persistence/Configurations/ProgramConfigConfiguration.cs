using KBeauty.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KBeauty.Loyalty.Infrastructure.Persistence.Configurations;

internal sealed class ProgramConfigConfiguration : IEntityTypeConfiguration<ProgramConfig>
{
    public void Configure(EntityTypeBuilder<ProgramConfig> builder)
    {
        builder.ToTable("ProgramConfigs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Value)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.UpdatedBy).HasMaxLength(100);

        builder.Property(c => c.UpdatedAt).HasColumnType("datetime2(3)");

        // Key única — toda la lógica del programa la indexa por nombre.
        builder.HasIndex(c => c.Key).IsUnique();
    }
}
