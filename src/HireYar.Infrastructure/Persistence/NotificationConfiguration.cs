using HireYar.Domain.Notifications;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasKey(n => n.Id);
        b.Property(n => n.Type).HasMaxLength(64).IsRequired();
        b.Property(n => n.SourceKey).HasMaxLength(100).IsRequired();
        b.Property(n => n.Target).HasMaxLength(40).IsRequired();
        b.Property(n => n.Title).HasMaxLength(500).IsRequired();
        b.HasIndex(n => new { n.OrganizationId, n.RecipientUserId, n.CreatedAtUtc, n.Id });
        b.HasIndex(n => new { n.OrganizationId, n.RecipientUserId, n.CreatedAtUtc }).HasFilter("\"ReadAtUtc\" IS NULL");
        b.HasIndex(n => new { n.OrganizationId, n.RecipientUserId, n.Type, n.SourceKey }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => new { n.OrganizationId, n.RecipientUserId })
            .HasPrincipalKey(u => new { u.OrganizationId, u.Id });
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(n => new { n.OrganizationId, n.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_Notification_ReadTime", "\"ReadAtUtc\" IS NULL OR \"ReadAtUtc\" >= \"CreatedAtUtc\""));
    }
}
