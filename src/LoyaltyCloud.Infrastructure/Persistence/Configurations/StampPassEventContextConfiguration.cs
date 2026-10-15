using LoyaltyCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoyaltyCloud.Infrastructure.Persistence.Configurations;

internal sealed class StampPassEventContextConfiguration : IEntityTypeConfiguration<StampPassEventContext>
{
    public void Configure(EntityTypeBuilder<StampPassEventContext> b)
    {
        b.ToTable("StampPassEventContexts");
        b.HasKey(x => x.Id);
        b.Property(x => x.SerialNumber).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.SerialNumber }).IsUnique();
        b.HasOne<LoyaltyCard>().WithMany().HasForeignKey(x => new { x.TenantId, x.SerialNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.SerialNumber }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StampTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, x.TransactionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
