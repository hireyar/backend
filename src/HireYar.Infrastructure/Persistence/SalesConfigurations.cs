using HireYar.Domain.Sales;
using HireYar.Domain.Organizations;
using HireYar.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;
public sealed class SalesConfigurations : IEntityTypeConfiguration<SalesAgent>, IEntityTypeConfiguration<SalesLead>,
    IEntityTypeConfiguration<SalesCommission>, IEntityTypeConfiguration<SalesPayout>, IEntityTypeConfiguration<SalesAudit>
{
    public void Configure(EntityTypeBuilder<SalesAgent> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => x.Mobile).IsUnique();
        b.Property(x => x.Mobile).HasMaxLength(11); b.Property(x => x.FullName).HasMaxLength(200); b.Property(x => x.PasswordHash).HasMaxLength(512);
        b.Property(x => x.FirstSalePercent).HasPrecision(5,2); b.Property(x => x.RenewalPercent).HasPrecision(5,2);
        b.ToTable(t => t.HasCheckConstraint("CK_SalesAgent_Values", "\"Mobile\" ~ '^09[0-9]{9}$' AND length(btrim(\"FullName\")) > 0 AND \"FirstSalePercent\" BETWEEN 0 AND 100 AND \"RenewalPercent\" BETWEEN 0 AND 100 AND \"FailedAttempts\" >= 0"));
    }
    public void Configure(EntityTypeBuilder<SalesLead> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Mobile).IsUnique(); b.HasIndex(x => x.OrganizationId).IsUnique(); b.HasIndex(x => new { x.AgentId, x.CreatedAtUtc });
        b.HasOne<SalesAgent>().WithMany().HasForeignKey(x => x.AgentId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.Property(x => x.Mobile).HasMaxLength(11); b.Property(x => x.CustomerName).HasMaxLength(200); b.Property(x => x.Note).HasMaxLength(2000); b.Property(x => x.ContactOrganizationName).HasMaxLength(200); b.Property(x => x.ContactEmail).HasMaxLength(256);
        b.ToTable(t => t.HasCheckConstraint("CK_SalesLead_Values", "\"Mobile\" ~ '^09[0-9]{9}$' AND length(btrim(\"CustomerName\")) > 0 AND ((\"OrganizationId\" IS NULL AND \"RegisteredAtUtc\" IS NULL) OR (\"OrganizationId\" IS NOT NULL AND \"RegisteredAtUtc\" IS NOT NULL))"));
    }
    public void Configure(EntityTypeBuilder<SalesCommission> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => x.OrderId).IsUnique(); b.HasIndex(x => new { x.AgentId, x.CreatedAtUtc });
        b.HasOne<SalesAgent>().WithMany().HasForeignKey(x => x.AgentId);
        b.HasOne<SalesLead>().WithMany().HasForeignKey(x => x.LeadId);
        b.HasOne<SubscriptionOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<SalesPayout>().WithMany().HasForeignKey(x => new { x.AgentId, x.PayoutId }).HasPrincipalKey(x => new { x.AgentId, x.Id });
        b.Property(x => x.Percent).HasPrecision(5,2); b.Property(x => x.Kind).HasMaxLength(20);
        b.ToTable(t => t.HasCheckConstraint("CK_SalesCommission_Values", "\"Kind\" IN ('FirstSale','Renewal') AND \"InvoiceAmountRial\" > 0 AND \"Percent\" BETWEEN 0 AND 100 AND \"AmountRial\" = floor(\"InvoiceAmountRial\"::numeric * \"Percent\" / 100) AND \"AvailableAtUtc\" = \"CreatedAtUtc\" + interval '168 hours'"));
    }
    public void Configure(EntityTypeBuilder<SalesPayout> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.AgentId, x.Id }); b.HasIndex(x => new { x.AgentId, x.RequestId }).IsUnique();
        b.HasOne<SalesAgent>().WithMany().HasForeignKey(x => x.AgentId); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ReviewNote).HasMaxLength(1000);
        b.ToTable(t => t.HasCheckConstraint("CK_SalesPayout_Values", "\"AmountRial\" > 0 AND ((\"Status\" = 'Requested' AND \"ReviewedAtUtc\" IS NULL AND \"ReviewedById\" IS NULL AND \"ReviewNote\" IS NULL) OR (\"Status\" IN ('Paid','Returned') AND \"ReviewedAtUtc\" IS NOT NULL AND \"ReviewedById\" IS NOT NULL AND length(btrim(\"ReviewNote\")) > 0))"));
    }
    public void Configure(EntityTypeBuilder<SalesAudit> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(160); b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasOne<SalesLead>().WithMany().HasForeignKey(x => x.LeadId);
    }
}
