using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyNotificationConfiguration : IEntityTypeConfiguration<LoyaltyNotification>
{
    public void Configure(EntityTypeBuilder<LoyaltyNotification> builder)
    {
        builder.ToTable("LoyaltyNotifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.ShortMessage).HasMaxLength(40);
        builder.Property(n => n.LongMessage).HasMaxLength(500);
        builder.Property(n => n.CorrelationId).HasMaxLength(200);
        builder.Property(n => n.Source).HasMaxLength(100);
        builder.Property(n => n.MetadataJson).HasMaxLength(4000);
        builder.Property(n => n.FailureReason).HasMaxLength(1000);

        builder.Property(n => n.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(n => n.ScheduledAtUtc).HasColumnType("datetime2(3)");
        builder.Property(n => n.ProcessingStartedAt).HasColumnType("datetime2(3)");
        builder.Property(n => n.ProcessedAt).HasColumnType("datetime2(3)");
        builder.Property(n => n.CancelledAt).HasColumnType("datetime2(3)");
        builder.Property(n => n.DisplayUntilUtc).HasColumnType("datetime2(3)");

        builder.HasMany(n => n.Deliveries)
            .WithOne()
            .HasPrincipalKey(n => new { n.TenantId, n.Id })
            .HasForeignKey(d => new { d.TenantId, d.LoyaltyNotificationId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(n => n.Deliveries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasForeignKey(n => new { n.TenantId, n.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LoyaltyCard>()
            .WithMany()
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasForeignKey(n => new { n.TenantId, n.LoyaltyCardId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CustomNotificationCampaign>()
            .WithMany()
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasForeignKey(n => new { n.TenantId, n.CustomNotificationCampaignId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.TenantId, n.Id }).IsUnique();
        builder.HasIndex(n => new { n.TenantId, n.CustomerId });
        builder.HasIndex(n => new { n.TenantId, n.LoyaltyCardId });
        builder.HasIndex(n => new { n.TenantId, n.CustomNotificationCampaignId });
        builder.HasIndex(n => new { n.TenantId, n.Status });
        builder.HasIndex(n => new { n.TenantId, n.ScheduledAtUtc });
        builder.HasIndex(n => new { n.TenantId, n.CreatedAt });
        builder.HasIndex(n => new { n.TenantId, n.CorrelationId })
            .IsUnique()
            .HasFilter("[CorrelationId] IS NOT NULL");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
