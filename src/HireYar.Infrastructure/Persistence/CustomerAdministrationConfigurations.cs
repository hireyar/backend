using HireYar.Domain.Administration;
using HireYar.Domain.Organizations;
using HireYar.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;
public sealed class CustomerAdministrationConfigurations : IEntityTypeConfiguration<CustomerAdministrationEvent>, IEntityTypeConfiguration<CustomerSubscriptionOverride>
{
    public void Configure(EntityTypeBuilder<CustomerAdministrationEvent> b)
    {
        b.HasKey(x=>x.Id); b.HasIndex(x=>new { x.CustomerId,x.RequestId }).IsUnique(); b.HasIndex(x=>new {x.OrganizationId,x.CreatedAtUtc});
        b.HasOne<Organization>().WithMany().HasForeignKey(x=>x.OrganizationId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ActorId);
        b.Property(x=>x.CustomerKind).HasMaxLength(10); b.Property(x=>x.Fingerprint).HasMaxLength(64); b.Property(x=>x.Action).HasMaxLength(64);
        b.Property(x=>x.Reason).HasMaxLength(1000); b.Property(x=>x.DetailsJson).HasColumnType("jsonb"); b.Property(x=>x.BundleCode).HasMaxLength(64);
        b.ToTable(t=>t.HasCheckConstraint("CK_CustomerAdminEvent_Values", "\"CustomerKind\" IN ('User','Lead') AND \"RequestId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(\"Reason\")) > 0 AND ((\"Action\"='InterviewCreditGranted' AND \"CreditQuantity\" BETWEEN 1 AND 100000 AND \"OrganizationId\" IS NOT NULL AND \"BundleCode\" IS NOT NULL) OR (\"Action\" IN ('ProfileUpdated','SellerChanged','SubscriptionSet','SubscriptionDaysAdjusted','SubscriptionStopped') AND \"CreditQuantity\"=0 AND \"BundleCode\" IS NULL))"));
    }
    public void Configure(EntityTypeBuilder<CustomerSubscriptionOverride> b)
    {
        b.HasKey(x=>x.OrganizationId); b.HasOne<Organization>().WithOne().HasForeignKey<CustomerSubscriptionOverride>(x=>x.OrganizationId);
        b.HasOne<CustomerAdministrationEvent>().WithOne().HasForeignKey<CustomerSubscriptionOverride>(x=>x.EventId);
        b.Property(x=>x.EventId).IsConcurrencyToken();
        b.ToTable(t=>t.HasCheckConstraint("CK_CustomerOverride_Values", "(\"IsStopped\" OR \"ExpiresAtUtc\" > \"StartedAtUtc\") AND \"TeamUsers\" > 0 AND \"RetentionDays\" BETWEEN 1 AND 3650"));
    }
}
