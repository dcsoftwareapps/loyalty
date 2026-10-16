using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class SelfServiceSignupAttemptConfiguration : IEntityTypeConfiguration<SelfServiceSignupAttempt>
{
    public void Configure(EntityTypeBuilder<SelfServiceSignupAttempt> builder)
    {
        builder.ToTable("SelfServiceSignupAttempts");
        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.AttemptId).HasMaxLength(100).IsRequired();
        builder.Property(attempt => attempt.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(attempt => attempt.PasswordVerificationHash).HasMaxLength(1000);
        builder.Property(attempt => attempt.TenantSlug).HasMaxLength(50);
        builder.Property(attempt => attempt.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(attempt => attempt.CompletedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(attempt => attempt.TrialEndsAtUtc).HasColumnType("datetime2(3)");

        builder.HasIndex(attempt => attempt.AttemptId).IsUnique();
        builder.HasOne(attempt => attempt.Tenant)
            .WithMany()
            .HasForeignKey(attempt => attempt.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
