using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("TenantBrandings");
        builder.HasKey(b => b.TenantId);

        builder.Property(b => b.LogoUrl).HasMaxLength(1000);
        builder.Property(b => b.PrimaryColor).HasMaxLength(20).IsRequired();
        builder.Property(b => b.SecondaryColor).HasMaxLength(20).IsRequired();
        builder.Property(b => b.SupportPhone).HasMaxLength(50);
        builder.Property(b => b.WhatsAppUrl).HasMaxLength(1000);
        builder.Property(b => b.InstagramUrl).HasMaxLength(1000);
        builder.Property(b => b.TermsUrl).HasMaxLength(1000);

    }
}
