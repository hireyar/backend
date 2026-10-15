using HireYar.Domain.Support;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> b)
    {
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => new { x.OrganizationId, x.UpdatedAtUtc, x.Id });
        b.HasIndex(x => new { x.Status, x.UpdatedAtUtc, x.Id });
        b.ToTable(t => {
            t.HasCheckConstraint("CK_SupportTicket_Values", "\"Status\" BETWEEN 0 AND 2 AND \"Priority\" BETWEEN 0 AND 2 AND \"Revision\" > 0");
            t.HasCheckConstraint("CK_SupportTicket_Subject", "length(btrim(\"Subject\")) BETWEEN 1 AND 200");
        });
    }
}
public sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> b)
    {
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.TicketId, x.Id });
        b.HasOne<SupportTicket>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.TicketId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        // Staff authors are platform actors; their account organization is not the ticket organization.
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId);
        b.Property(x => x.Body).HasMaxLength(8000);
        b.HasIndex(x => new { x.OrganizationId, x.TicketId, x.CreatedAtUtc, x.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_TicketMessage_Body", "length(btrim(\"Body\")) BETWEEN 1 AND 8000"));
    }
}
public sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<TicketMessage>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.TicketId, x.MessageId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.TicketId, x.Id });
        b.Property(x => x.FileName).HasMaxLength(180);
        b.Property(x => x.ContentType).HasMaxLength(32);
        b.Property(x => x.StorageKey).HasMaxLength(73);
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.ToTable(t => {
            t.HasCheckConstraint("CK_TicketAttachment_Length", "\"Length\" BETWEEN 1 AND 5242880");
            t.HasCheckConstraint("CK_TicketAttachment_Type", "\"ContentType\" IN ('image/png','image/jpeg','application/pdf','text/plain')");
        });
    }
}

