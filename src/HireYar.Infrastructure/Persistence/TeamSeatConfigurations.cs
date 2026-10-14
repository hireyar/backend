using HireYar.Domain.Billing;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Infrastructure.Persistence;

internal static class TeamSeatConfigurations
{
    internal static void Configure(ModelBuilder m)
    {
        var package = m.Entity<PricingPackage>();
        package.Property(x => x.TeamUserMonthlyPriceIrt).HasPrecision(18, 0);
        package.ToTable(t => t.HasCheckConstraint("CK_Package_Team", "\"IncludedTeamUsers\" >= 1 AND \"PaidTeamUserLimit\" BETWEEN \"IncludedTeamUsers\" AND 10000 AND \"TeamUserMonthlyPriceIrt\" BETWEEN 0 AND 1000000000000"));
        var purchase = m.Entity<SubscriptionPurchase>();
        purchase.Property(x => x.TeamUserMonthlyPriceIrtSnapshot).HasPrecision(18, 0);
        purchase.Property(x => x.TeamSeatAmountIrtSnapshot).HasPrecision(18, 0);
        purchase.ToTable(t => t.HasCheckConstraint("CK_Purchase_Team", "\"IncludedTeamUsersSnapshot\" >= 1 AND \"PaidTeamUserLimitSnapshot\" BETWEEN \"IncludedTeamUsersSnapshot\" AND 10000 AND \"TeamUserMonthlyPriceIrtSnapshot\" BETWEEN 0 AND 1000000000000 AND \"TeamCapacitySnapshot\" BETWEEN \"IncludedTeamUsersSnapshot\" AND \"PaidTeamUserLimitSnapshot\" AND \"TeamSeatAmountIrtSnapshot\" >= 0"));
        var trial = m.Entity<RegistrationTrial>();
        trial.Property(x => x.TeamUserMonthlyPriceIrt).HasPrecision(18, 0);
        trial.ToTable(t => t.HasCheckConstraint("CK_Trial_Team", "\"IncludedTeamUsers\" >= 1 AND \"PaidTeamUserLimit\" BETWEEN \"IncludedTeamUsers\" AND 10000 AND \"TeamUserMonthlyPriceIrt\" BETWEEN 0 AND 1000000000000"));
        var seat = m.Entity<TeamSeatPurchase>();
        seat.HasKey(x => x.Id); seat.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        seat.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        seat.HasOne<Payment>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PaymentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        seat.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SubscriptionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        seat.HasIndex(x => x.PaymentId).IsUnique(); seat.HasIndex(x => new { x.OrganizationId, x.SubscriptionId });
        seat.Property(x => x.MonthlyPriceIrt).HasPrecision(18, 0); seat.Property(x => x.AmountIrt).HasPrecision(18, 0);
        seat.ToTable(t => t.HasCheckConstraint("CK_TeamSeat_Values", "\"PreviousCapacity\" >= 1 AND \"TargetCapacity\" BETWEEN \"PreviousCapacity\" + 1 AND 10000 AND \"MonthlyPriceIrt\" > 0 AND \"AmountIrt\" > 0 AND \"ExpiresAtUtc\" > \"QuotedAtUtc\" AND \"CreatedAtUtc\" >= \"QuotedAtUtc\""));
    }
}
