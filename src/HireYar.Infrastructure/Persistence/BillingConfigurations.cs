using HireYar.Domain.Billing;
using HireYar.Domain.Interviews;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;

internal static class BillingConfigurations
{
    private static void Money<T>(EntityTypeBuilder<T> b, string name, int scale = 6) where T : class => b.Property<decimal>(name).HasPrecision(18, scale);
    private static void Tenant<T>(EntityTypeBuilder<T> b) where T : class
    {
        b.HasKey("Id"); b.HasAlternateKey("OrganizationId", "Id");
        b.HasOne<Organization>().WithMany().HasForeignKey("OrganizationId").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex("OrganizationId", "CreatedAtUtc", "Id");
    }
    public static void Configure(ModelBuilder m)
    {
        var p = m.Entity<PricingPackage>(); p.HasKey(x => x.Id); p.HasIndex(x => x.Code).IsUnique();
        p.Property(x => x.Code).HasMaxLength(64); p.Property(x => x.Name).HasMaxLength(200); p.Property(x => x.Description).HasMaxLength(4000);
        p.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever(); Money(p, nameof(PricingPackage.AiPricePerMinuteUsd));
        p.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.PackageId);
        p.HasMany(x => x.Features).WithOne().HasForeignKey(x => x.PackageId);
        p.ToTable(t => t.HasCheckConstraint("CK_Package_Values", "\"AiPricePerMinuteUsd\" > 0 AND \"InterviewRetentionDays\" BETWEEN 1 AND 3650"));
        var period = m.Entity<PackagePeriod>(); period.HasKey(x => x.Id); period.HasAlternateKey(x => new { x.PackageId, x.Id });
        period.HasIndex(x => new { x.PackageId, x.Months }).IsUnique(); Money(period, nameof(PackagePeriod.PriceAmount), 0); Money(period, nameof(PackagePeriod.DiscountPercent), 2);
        period.Property(x => x.Currency).HasMaxLength(3);
        period.ToTable(t => t.HasCheckConstraint("CK_Period_Values", "\"Months\" BETWEEN 1 AND 120 AND \"PriceAmount\" > 0 AND \"Currency\" = 'IRT' AND \"DiscountPercent\" BETWEEN 0 AND 100"));
        var f = m.Entity<FeatureDefinition>(); f.HasKey(x => x.Id); f.HasIndex(x => x.Code).IsUnique();
        f.Property(x => x.Code).HasMaxLength(64); f.Property(x => x.Name).HasMaxLength(200); f.Property(x => x.Description).HasMaxLength(4000);
        f.Property(x => x.DataType).HasConversion<string>().HasMaxLength(20);
        f.ToTable(t => t.HasCheckConstraint("CK_Feature_Type", "\"DataType\" IN ('Boolean','Integer','Text') AND \"Code\" <> 'interview_retention_days'"));
        var pf = m.Entity<PackageFeature>(); pf.HasKey(x => new { x.PackageId, x.FeatureId }); pf.Property(x => x.Value).HasMaxLength(1000);
        pf.HasOne(x => x.Feature).WithMany().HasForeignKey(x => x.FeatureId);
        var c = m.Entity<AiCreditPackage>(); c.HasKey(x => x.Id); c.Property(x => x.Name).HasMaxLength(200); c.Property(x => x.SellPriceCurrency).HasMaxLength(3);
        Money(c, nameof(AiCreditPackage.CreditAmountUsd)); Money(c, nameof(AiCreditPackage.SellPriceAmount), 0); c.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        c.ToTable(t => t.HasCheckConstraint("CK_CreditProduct_Values", "\"CreditAmountUsd\" > 0 AND \"SellPriceAmount\" > 0 AND \"SellPriceCurrency\" = 'IRT'"));
        var pay = m.Entity<Payment>(); Tenant(pay); Money(pay, nameof(Payment.Amount), 0);
        pay.Property(x => x.CreditAmountUsdSnapshot).HasPrecision(18, 6); pay.Property(x => x.EffectiveUsdToIrtSnapshot).HasPrecision(24, 6);
        pay.Property(x => x.Type).HasConversion<string>().HasMaxLength(20); pay.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        pay.Property(x => x.Currency).HasMaxLength(3); pay.Property(x => x.Provider).HasMaxLength(64);
        pay.Property(x => x.ProviderAuthority).HasMaxLength(512); pay.Property(x => x.ProviderTransactionId).HasMaxLength(200); pay.Property(x => x.CreditPackageNameSnapshot).HasMaxLength(200);
        pay.HasOne<AiCreditPackage>().WithMany().HasForeignKey(x => x.CreditPackageId);
        pay.HasIndex(x => new { x.Provider, x.ProviderTransactionId }).IsUnique().HasFilter("\"ProviderTransactionId\" IS NOT NULL");
        pay.HasIndex(x => new { x.Provider, x.ProviderAuthority }).IsUnique().HasFilter("\"ProviderAuthority\" IS NOT NULL");
        pay.ToTable(t => t.HasCheckConstraint("CK_Payment_Values", "\"Amount\" > 0 AND \"Currency\" = 'IRT' AND \"Status\" IN ('Pending','Paid','Failed','Cancelled','Expired') AND \"Type\" IN ('Subscription','AiCredit') AND (\"Type\" <> 'AiCredit' OR (\"CreditPackageId\" IS NOT NULL AND \"CreditAmountUsdSnapshot\" > 0 AND \"EffectiveUsdToIrtSnapshot\" > 0)) AND (\"Status\" <> 'Paid' OR (\"PaidAtUtc\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL))"));
        var purchase = m.Entity<SubscriptionPurchase>(); Tenant(purchase); purchase.HasIndex(x => x.PaymentId).IsUnique();
        purchase.HasOne<Payment>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PaymentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        purchase.HasOne<PackagePeriod>().WithMany().HasForeignKey(x => new { x.PackageId, x.PackagePeriodId }).HasPrincipalKey(x => new { x.PackageId, x.Id });
        purchase.Property(x => x.PackageCodeSnapshot).HasMaxLength(64); purchase.Property(x => x.PackageNameSnapshot).HasMaxLength(200); purchase.Property(x => x.CurrencySnapshot).HasMaxLength(3);
        Money(purchase, nameof(SubscriptionPurchase.PriceAmountSnapshot), 0); Money(purchase, nameof(SubscriptionPurchase.DiscountPercentSnapshot), 2); Money(purchase, nameof(SubscriptionPurchase.AiPricePerMinuteUsdSnapshot));
        purchase.HasMany(x => x.Entitlements).WithOne().HasForeignKey(x => x.PurchaseId);
        purchase.ToTable(t => t.HasCheckConstraint("CK_Purchase_Values", "\"PeriodMonthsSnapshot\" BETWEEN 1 AND 120 AND \"PriceAmountSnapshot\" > 0 AND \"CurrencySnapshot\" = 'IRT' AND \"AiPricePerMinuteUsdSnapshot\" > 0 AND \"RetentionDaysSnapshot\" > 0"));
        var e = m.Entity<SubscriptionEntitlement>(); e.HasKey(x => new { x.PurchaseId, x.Code }); e.Property(x => x.Code).HasMaxLength(64); e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Value).HasMaxLength(1000); e.Property(x => x.DataType).HasConversion<string>().HasMaxLength(20);
        var sub = m.Entity<OrganizationSubscription>(); Tenant(sub); sub.HasIndex(x => x.PaymentId).IsUnique();
        sub.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        sub.HasOne<Payment>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PaymentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        sub.HasOne<PackagePeriod>().WithMany().HasForeignKey(x => new { x.PackageId, x.PackagePeriodId }).HasPrincipalKey(x => new { x.PackageId, x.Id });
        sub.HasIndex(x => new { x.OrganizationId, x.StartedAtUtc, x.ExpiresAtUtc });
        sub.ToTable(t => t.HasCheckConstraint("CK_Subscription_Values", "\"ExpiresAtUtc\" > \"StartedAtUtc\" AND \"Status\" IN ('Scheduled','Active','Expired','Cancelled','Suspended')"));
        var w = m.Entity<OrganizationAiWallet>(); Tenant(w); w.HasIndex(x => x.OrganizationId).IsUnique(); w.Property(x => x.Currency).HasMaxLength(3); Money(w, nameof(OrganizationAiWallet.Balance));
        w.ToTable(t => t.HasCheckConstraint("CK_Wallet_Values", "\"Balance\" >= 0 AND \"Currency\" = 'USD'"));
        var tx = m.Entity<AiCreditTransaction>(); Tenant(tx); tx.Property(x => x.Type).HasConversion<string>().HasMaxLength(30); tx.Property(x => x.ReferenceType).HasMaxLength(40); tx.Property(x => x.Description).HasMaxLength(1000);
        Money(tx, nameof(AiCreditTransaction.Amount)); Money(tx, nameof(AiCreditTransaction.BalanceBefore)); Money(tx, nameof(AiCreditTransaction.BalanceAfter));
        tx.HasOne<OrganizationAiWallet>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.WalletId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        tx.HasIndex(x => new { x.OrganizationId, x.ReferenceType, x.ReferenceId }).IsUnique();
        tx.ToTable(t => t.HasCheckConstraint("CK_Ledger_Values", "\"BalanceBefore\" >= 0 AND \"BalanceAfter\" >= 0 AND \"BalanceAfter\" = \"BalanceBefore\" + \"Amount\" AND \"Type\" IN ('CreditPurchase','InterviewUsage','Refund','AdminAdjustment','PromotionalCredit','Correction')"));
        var r = m.Entity<AiCreditReservation>(); Tenant(r); r.HasIndex(x => x.InterviewSessionId).IsUnique(); r.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        Money(r, nameof(AiCreditReservation.ReservedAmountUsd)); Money(r, nameof(AiCreditReservation.ConsumedAmountUsd));
        r.HasOne<OrganizationAiWallet>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.WalletId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        r.HasOne<InterviewSession>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.InterviewSessionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        r.ToTable(t => t.HasCheckConstraint("CK_Reservation_Values", "\"ReservedAmountUsd\" >= 0 AND \"ConsumedAmountUsd\" BETWEEN 0 AND \"ReservedAmountUsd\" AND \"Status\" IN ('Active','Consumed','Released','Expired')"));
        var ib = m.Entity<InterviewBilling>(); Tenant(ib); ib.HasIndex(x => x.InterviewSessionId).IsUnique(); ib.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        Money(ib, nameof(InterviewBilling.RatePerMinuteUsd)); Money(ib, nameof(InterviewBilling.BilledAmountUsd));
        ib.HasOne<InterviewSession>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.InterviewSessionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        ib.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SubscriptionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        ib.ToTable(t => t.HasCheckConstraint("CK_InterviewBilling_Values", "\"RatePerMinuteUsd\" > 0 AND \"BillableSeconds\" BETWEEN 0 AND 3600 AND \"BilledAmountUsd\" >= 0 AND \"Status\" IN ('Reserved','Settled','Released')"));
    }
}
