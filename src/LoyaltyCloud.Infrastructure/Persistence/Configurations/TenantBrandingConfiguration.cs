using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
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
        builder.Property(b => b.LogoBlobName).HasMaxLength(500);
        builder.Property(b => b.WalletBackgroundColor).HasMaxLength(7);
        builder.Property(b => b.WalletLogoBlobName).HasMaxLength(500);
        builder.Property(b => b.WalletLogoScalePercent)
            .HasDefaultValue(TenantBranding.DefaultWalletLogoScalePercent)
            .IsRequired();
        builder.Property(b => b.AppleWalletPrimaryContentMode)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(AppleWalletPrimaryContentMode.CustomerName)
            .IsRequired();
        builder.Property(b => b.AppleWalletStripImageBlobName).HasMaxLength(500);
        builder.Property(b => b.PrimaryColor).HasMaxLength(TenantBranding.ColorMaxLength).IsRequired();
        builder.Property(b => b.SecondaryColor).HasMaxLength(TenantBranding.ColorMaxLength).IsRequired();
        builder.Property(b => b.SupportPhone).HasMaxLength(TenantBranding.SupportPhoneMaxLength);
        builder.Property(b => b.WhatsAppUrl).HasMaxLength(TenantBranding.UrlMaxLength);
        builder.Property(b => b.InstagramUrl).HasMaxLength(TenantBranding.UrlMaxLength);
        builder.Property(b => b.TermsUrl).HasMaxLength(TenantBranding.UrlMaxLength);

    }
}
