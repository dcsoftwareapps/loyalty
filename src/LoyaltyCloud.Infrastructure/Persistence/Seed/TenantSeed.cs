using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Persistence.Seed;

public static class TenantSeed
{
    public static readonly Guid KBeautyTenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly DateTime SeedDate = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasData(new
        {
            Id = KBeautyTenantId,
            Slug = "kbeauty",
            DisplayName = "KBeauty",
            IsActive = true,
            TimeZoneId = "America/Tijuana",
            CreatedAt = SeedDate,
            UpdatedAt = (DateTime?)null
        });

        modelBuilder.Entity<TenantBranding>().HasData(new
        {
            TenantId = KBeautyTenantId,
            LogoUrl = (string?)null,
            PrimaryColor = "#1C1C1C",
            SecondaryColor = "#E8668E",
            SupportPhone = (string?)null,
            WhatsAppUrl = (string?)null,
            InstagramUrl = (string?)null,
            TermsUrl = (string?)null
        });

        modelBuilder.Entity<TenantSubscription>().HasData(new
        {
            TenantId = KBeautyTenantId,
            Status = TenantSubscriptionStatus.Active,
            PlanCode = "internal",
            CurrentPeriodStart = (DateTime?)null,
            CurrentPeriodEnd = (DateTime?)null,
            GracePeriodEndsAt = (DateTime?)null,
            LastPaymentAt = (DateTime?)null
        });
    }
}
