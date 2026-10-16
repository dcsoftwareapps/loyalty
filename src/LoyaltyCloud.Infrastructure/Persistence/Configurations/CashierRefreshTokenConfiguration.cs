using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class CashierRefreshTokenConfiguration : IEntityTypeConfiguration<CashierRefreshToken>
{
    public void Configure(EntityTypeBuilder<CashierRefreshToken> b)
    {
        b.ToTable("CashierRefreshTokens"); b.HasKey(t=>t.Id);
        b.Property(t=>t.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.HasIndex(t=>t.TokenHash).IsUnique();
        b.HasIndex(t=>new { t.TenantId, t.TenantAdminUserId });
        b.Property(t=>t.RevokedAtUtc).IsConcurrencyToken();
        b.HasOne<TenantAdminUser>().WithMany().HasForeignKey(t=>new { t.TenantId,t.TenantAdminUserId })
            .HasPrincipalKey(u=>new { u.TenantId,u.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
