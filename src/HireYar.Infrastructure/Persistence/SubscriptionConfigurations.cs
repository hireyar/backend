using HireYar.Domain.Subscriptions;
using HireYar.Domain.Organizations;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;

public sealed class SubscriptionConfigurations : IEntityTypeConfiguration<SubscriptionPlan>, IEntityTypeConfiguration<InterviewBundle>,
    IEntityTypeConfiguration<SubscriptionOrder>, IEntityTypeConfiguration<OrganizationMembership>,
    IEntityTypeConfiguration<InterviewQuotaGrant>, IEntityTypeConfiguration<InterviewQuotaReservation>, IEntityTypeConfiguration<SignupTrial>
{
    public void Configure(EntityTypeBuilder<SignupTrial> b)
    {
        b.HasKey(t => t.OrganizationId);
        b.HasOne<Organization>().WithOne().HasForeignKey<SignupTrial>(t => t.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_SignupTrial_Values",
            "\"ExpiresAtUtc\" = \"StartedAtUtc\" + interval '48 hours' AND \"InterviewQuantity\" = 2 AND \"TeamUsers\" >= 1 AND \"RetentionDays\" BETWEEN 1 AND 3650"));
    }
    private static void Tenant<T>(EntityTypeBuilder<T> b) where T : class
    {
        b.HasKey("Id"); b.HasAlternateKey("OrganizationId", "Id");
        b.HasOne<Organization>().WithMany().HasForeignKey("OrganizationId");
    }
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.HasKey(p => p.Id); b.Property(p => p.Name).HasMaxLength(200); b.Property(p => p.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        b.ToTable(t => {
            t.HasCheckConstraint("CK_SubscriptionPlan_Singleton", "\"Id\" = 'd1000000-0000-0000-0000-000000000001'::uuid");
            t.HasCheckConstraint("CK_SubscriptionPlan_Values", "\"MonthlyPriceRial\" BETWEEN 1 AND 1000000000000 AND \"BaseInterviewPriceRial\" BETWEEN 1 AND 1000000000000 AND \"ExtraUserMonthlyPriceRial\" BETWEEN 0 AND 1000000000000 AND \"IncludedUsers\" >= 1 AND \"PaidUserCeiling\" > \"IncludedUsers\" AND \"RetentionDays\" BETWEEN 1 AND 3650");
        });
        b.HasData(new SubscriptionPlan { RowVersion = Guid.Parse("d1000000-0000-0000-0000-000000000002").ToByteArray() });
    }
    public void Configure(EntityTypeBuilder<InterviewBundle> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => x.Code).IsUnique(); b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever(); b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.PlanId);
        b.ToTable(t => t.HasCheckConstraint("CK_InterviewBundle_Values", "\"MinimumQuantity\" BETWEEN 1 AND 1000000 AND \"MaximumQuantity\" BETWEEN \"MinimumQuantity\" AND 1000000 AND \"QuantityStep\" >= 1 AND (\"MaximumQuantity\" - \"MinimumQuantity\") % \"QuantityStep\" = 0 AND \"DiscountPercent\" BETWEEN 0 AND 99.99"));
        var rows = new[] { ("dana", "دانا", 10, 10, 1, 0m), ("bina", "بینا", 20, 20, 1, 6.25m),
            ("zharfa", "ژرفا", 50, 50, 1, 5m), ("custom", "دلخواه", 50, 300, 10, 0m) };
        b.HasData(rows.Select((r, i) => new InterviewBundle { Id = Guid.Parse($"d2000000-0000-0000-0000-{i+1:000000000000}"),
            Code = r.Item1, Name = r.Item2, MinimumQuantity = r.Item3, MaximumQuantity = r.Item4, QuantityStep = r.Item5,
            DiscountPercent = r.Item6, SortOrder = i + 1, RowVersion = Guid.Parse($"d3000000-0000-0000-0000-{i+1:000000000000}").ToByteArray() }));
    }
    public void Configure(EntityTypeBuilder<SubscriptionOrder> b)
    {
        Tenant(b); b.HasIndex(o => new { o.OrganizationId, o.RequestId }).IsUnique();
        b.HasIndex(o => o.PaymentReference).IsUnique().HasFilter("\"PaymentReference\" IS NOT NULL");
        b.HasIndex(o => new { o.OrganizationId, o.CreatedAtUtc }); b.Property(o => o.RequestFingerprint).HasMaxLength(64);
        b.Property(o => o.Product).HasMaxLength(20); b.Property(o => o.Status).HasMaxLength(20); b.Property(o => o.ProductName).HasMaxLength(200);
        b.Property(o => o.BundleCode).HasMaxLength(64); b.Property(o => o.PaymentReference).HasMaxLength(120); b.Property(o => o.DiscountPercent).HasPrecision(5, 2);
        b.ToTable(t => {
            t.HasCheckConstraint("CK_SubscriptionOrder_Values", "\"AmountRial\" > 0 AND \"OriginalAmountRial\" >= \"AmountRial\" AND \"ExpiresAtUtc\" > \"CreatedAtUtc\" AND \"Status\" IN ('Pending','Confirmed','Cancelled') AND \"Product\" IN ('subscription','interviews')");
            t.HasCheckConstraint("CK_SubscriptionOrder_Product", "(\"Product\"='subscription' AND \"Months\" IN (1,3,6,12) AND \"TeamUsers\" >= 1 AND \"Quantity\"=0 AND \"BundleCode\" IS NULL) OR (\"Product\"='interviews' AND \"Quantity\" > 0 AND \"Months\"=0 AND \"TeamUsers\"=0 AND \"BundleCode\" IS NOT NULL)");
            t.HasCheckConstraint("CK_SubscriptionOrder_Confirmation", "(\"Status\"='Confirmed' AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"PaymentReference\" IS NOT NULL AND length(btrim(\"PaymentReference\")) > 0) OR (\"Status\" <> 'Confirmed' AND \"ConfirmedAtUtc\" IS NULL AND \"ConfirmedByUserId\" IS NULL AND \"PaymentReference\" IS NULL)");
        });
    }
    public void Configure(EntityTypeBuilder<OrganizationMembership> b)
    {
        Tenant(b); b.HasIndex(m => m.OrderId).IsUnique(); b.HasIndex(m => new { m.OrganizationId, m.StartedAtUtc, m.ExpiresAtUtc });
        b.HasOne<SubscriptionOrder>().WithMany().HasForeignKey(m => new { m.OrganizationId, m.OrderId }).HasPrincipalKey(o => new { o.OrganizationId, o.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_Membership_Values", "\"ExpiresAtUtc\" > \"StartedAtUtc\" AND \"TeamUsers\" > 0 AND \"RetentionDays\" BETWEEN 1 AND 3650"));
    }
    public void Configure(EntityTypeBuilder<InterviewQuotaGrant> b)
    {
        Tenant(b); b.HasIndex(g => g.OrderId).IsUnique();
        b.HasOne<SubscriptionOrder>().WithMany().HasForeignKey(g => new { g.OrganizationId, g.OrderId }).HasPrincipalKey(o => new { o.OrganizationId, o.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_QuotaGrant_Quantity", "\"Quantity\" > 0"));
    }
    public void Configure(EntityTypeBuilder<InterviewQuotaReservation> b)
    {
        Tenant(b); b.HasIndex(r => r.InterviewSessionId).IsUnique(); b.HasIndex(r => new { r.OrganizationId, r.State }); b.Property(r => r.State).HasMaxLength(20);
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(r => new { r.OrganizationId, r.InterviewSessionId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.ToTable(t => {
            t.HasCheckConstraint("CK_QuotaReservation_State", "(\"State\"='Reserved' AND \"ConsumedAtUtc\" IS NULL AND \"ReleasedAtUtc\" IS NULL) OR (\"State\"='Consumed' AND \"ConsumedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" IS NULL) OR (\"State\"='Released' AND \"ReleasedAtUtc\" IS NOT NULL AND \"ConsumedAtUtc\" IS NULL)");
            t.HasCheckConstraint("CK_QuotaReservation_Retention", "\"RetentionDays\" BETWEEN 1 AND 3650");
        });
    }
}
