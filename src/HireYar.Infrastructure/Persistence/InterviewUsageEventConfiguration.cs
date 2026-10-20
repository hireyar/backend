using HireYar.Domain.Interviews;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class InterviewUsageEventConfiguration : IEntityTypeConfiguration<InterviewUsageEvent>
{
    public void Configure(EntityTypeBuilder<InterviewUsageEvent> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.EventType).HasMaxLength(32);
        b.HasIndex(e => new { e.OrganizationId, e.OccurredAtUtc, e.Id });
        b.HasIndex(e => new { e.InterviewSessionId, e.EventType }).IsUnique();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(e => new { e.OrganizationId, e.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id }).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_InterviewUsageEvent_Type",
            "\"EventType\" IN ('Created','Started','Completed','Cancelled','Expired','Replaced','AccessRevoked','Deleted','Reserved','CreditConsumed','Released')"));
    }
}
