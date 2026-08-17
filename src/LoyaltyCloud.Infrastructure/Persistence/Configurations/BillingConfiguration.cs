using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class BillingSettingsConfiguration : IEntityTypeConfiguration<BillingSettings>
{
    public void Configure(EntityTypeBuilder<BillingSettings> b) { b.ToTable("BillingSettings"); b.HasKey(x=>x.Id); b.Property(x=>x.Code).HasMaxLength(30).IsRequired(); b.HasIndex(x=>x.Code).IsUnique(); b.Property(x=>x.Currency).HasMaxLength(3); b.Property(x=>x.TaxRate).HasPrecision(5,2); b.Property(x=>x.BankName).HasMaxLength(150); b.Property(x=>x.BeneficiaryName).HasMaxLength(200); b.Property(x=>x.Clabe).HasMaxLength(18); b.Property(x=>x.SupportEmail).HasMaxLength(320); }
}
internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b) { b.ToTable("SubscriptionPlans"); b.HasKey(x=>x.Id); b.Property(x=>x.Code).HasMaxLength(50); b.HasIndex(x=>x.Code).IsUnique(); b.Property(x=>x.Name).HasMaxLength(150); b.Property(x=>x.Currency).HasMaxLength(3); b.Property(x=>x.MonthlyPrice).HasPrecision(18,2); b.Property(x=>x.ThreeMonthPrice).HasPrecision(18,2); b.Property(x=>x.SixMonthPrice).HasPrecision(18,2); b.Property(x=>x.TwelveMonthPrice).HasPrecision(18,2); }
}
internal sealed class BillingOrderConfiguration : IEntityTypeConfiguration<BillingOrder>
{
    public void Configure(EntityTypeBuilder<BillingOrder> b) { b.ToTable("BillingOrders", t=>{t.HasCheckConstraint("CK_BillingOrders_Months","[Months] IN (1,3,6,12)");t.HasCheckConstraint("CK_BillingOrders_Amounts","[Subtotal]>=0 AND [Tax]>=0 AND [Total]>=0");}); b.HasKey(x=>x.Id); b.Property(x=>x.PlanCode).HasMaxLength(50); b.Property(x=>x.Currency).HasMaxLength(3); b.Property(x=>x.Subtotal).HasPrecision(18,2); b.Property(x=>x.Tax).HasPrecision(18,2); b.Property(x=>x.Total).HasPrecision(18,2); b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x=>x.PaymentMethod).HasConversion<string>().HasMaxLength(30); b.Property(x=>x.Provider).HasConversion<string>().HasMaxLength(30); b.Property(x=>x.ExternalCheckoutId).HasMaxLength(200); b.Property(x=>x.BankReference).HasMaxLength(30); b.Property(x=>x.ApprovedBy).HasMaxLength(200); b.HasOne<Tenant>().WithMany().HasForeignKey(x=>x.TenantId).OnDelete(DeleteBehavior.Cascade); b.HasIndex(x=>new{x.TenantId,x.CreatedAt}); b.HasIndex(x=>x.ExternalCheckoutId).IsUnique().HasFilter("[ExternalCheckoutId] IS NOT NULL"); b.HasIndex(x=>x.BankReference).IsUnique().HasFilter("[BankReference] IS NOT NULL"); }
}
internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b) { b.ToTable("PaymentTransactions"); b.HasKey(x=>x.Id); b.Property(x=>x.Provider).HasConversion<string>().HasMaxLength(30); b.Property(x=>x.PaymentMethod).HasConversion<string>().HasMaxLength(30); b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30); b.Property(x=>x.ExternalTransactionId).HasMaxLength(200); b.Property(x=>x.Amount).HasPrecision(18,2); b.Property(x=>x.Currency).HasMaxLength(3); b.Property(x=>x.CardBrand).HasMaxLength(30); b.Property(x=>x.CardLast4).HasMaxLength(4); b.HasOne<BillingOrder>().WithMany().HasForeignKey(x=>x.BillingOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Tenant>().WithMany().HasForeignKey(x=>x.TenantId).OnDelete(DeleteBehavior.NoAction); b.HasIndex(x=>new{x.Provider,x.ExternalTransactionId}).IsUnique(); b.HasIndex(x=>new{x.TenantId,x.CreatedAt}); }
}
internal sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> b) { b.ToTable("PaymentWebhookEvents"); b.HasKey(x=>x.Id); b.Property(x=>x.Provider).HasConversion<string>().HasMaxLength(30); b.Property(x=>x.ProviderEventId).HasMaxLength(200); b.Property(x=>x.EventType).HasMaxLength(100); b.Property(x=>x.ProcessingStatus).HasConversion<string>().HasMaxLength(30); b.HasIndex(x=>new{x.Provider,x.ProviderEventId}).IsUnique(); }
}
