using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions");
        builder.HasKey(s => s.TenantId);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.SuspensionReason)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.PlanCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CurrentPeriodStart).HasColumnType("datetime2(3)");
        builder.Property(s => s.CurrentPeriodEnd).HasColumnType("datetime2(3)");
        builder.Property(s => s.PaidThroughUtc).HasColumnType("datetime2(3)");
        builder.Property(s => s.GracePeriodEndsAt).HasColumnType("datetime2(3)");
        builder.Property(s => s.LastPaymentAt).HasColumnType("datetime2(3)");

        builder.HasIndex(s => s.Status);
    }
}
