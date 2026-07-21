using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Channel).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(d => d.ProviderReference).HasMaxLength(200);
        builder.Property(d => d.FailureReason).HasMaxLength(1000);

        builder.Property(d => d.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(d => d.AttemptedAt).HasColumnType("datetime2(3)");
        builder.Property(d => d.CompletedAt).HasColumnType("datetime2(3)");

        builder.HasIndex(d => new { d.TenantId, d.LoyaltyNotificationId });
        builder.HasIndex(d => new { d.TenantId, d.Channel });
        builder.HasIndex(d => new { d.TenantId, d.Status });

        builder.HasOne<LoyaltyNotification>()
            .WithMany()
            .HasPrincipalKey(n => new { n.TenantId, n.Id })
            .HasForeignKey(d => new { d.TenantId, d.LoyaltyNotificationId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
