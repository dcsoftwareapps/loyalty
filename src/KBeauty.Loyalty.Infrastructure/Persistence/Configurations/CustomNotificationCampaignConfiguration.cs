using KBeauty.Loyalty.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KBeauty.Loyalty.Infrastructure.Persistence.Configurations;

internal sealed class CustomNotificationCampaignConfiguration : IEntityTypeConfiguration<CustomNotificationCampaign>
{
    public void Configure(EntityTypeBuilder<CustomNotificationCampaign> builder)
    {
        builder.ToTable("CustomNotificationCampaigns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(80).IsRequired();
        builder.Property(c => c.ShortMessage).HasMaxLength(40).IsRequired();
        builder.Property(c => c.LongMessage).HasMaxLength(500).IsRequired();
        builder.Property(c => c.AudienceType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.FailureReason).HasMaxLength(1000);

        builder.Property(c => c.ScheduledAtUtc).HasColumnType("datetime2(3)");
        builder.Property(c => c.DisplayUntilUtc).HasColumnType("datetime2(3)");
        builder.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(c => c.StartedAt).HasColumnType("datetime2(3)");
        builder.Property(c => c.CompletedAt).HasColumnType("datetime2(3)");
        builder.Property(c => c.CancelledAt).HasColumnType("datetime2(3)");

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.ScheduledAtUtc);
        builder.HasIndex(c => c.CreatedAt);
        builder.HasIndex(c => new { c.Status, c.ScheduledAtUtc });
    }
}
