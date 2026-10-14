using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HireYar.Application.Billing;

public sealed class RegistrationTrialService(IApplicationDbContext db, IOptions<RegistrationTrialOptions> options)
{
    // Called only inside the signup transaction for its newly inserted organization.
    // The new organization is not visible to other transactions before commit.
    public async Task CreateForNewOrganizationAsync(Guid org, DateTimeOffset registeredAtUtc, CancellationToken ct)
    {
        var settings = options.Value;
        var packages = await db.PricingCatalog.AsNoTracking().Include(x => x.Periods).Include(x => x.Features).ThenInclude(x => x.Feature)
            .Where(x => x.IsActive && x.IsPublic).OrderByDescending(x => x.InterviewRetentionDays).ThenBy(x => x.Code).ToListAsync(ct);
        var anchor = packages.FirstOrDefault(x => x.Periods.Any())
            ?? throw new AppException(503, "trial_catalog_unavailable", "The trial catalog is unavailable.");
        var subscription = new OrganizationSubscription { OrganizationId = org, IsTrial = true, PackageId = anchor.Id,
            PackagePeriodId = anchor.Periods.OrderBy(x => x.Months).First().Id, Status = SubscriptionStatus.Active,
            StartedAtUtc = registeredAtUtc, ExpiresAtUtc = registeredAtUtc.AddDays(settings.DurationDays), CreatedAtUtc = registeredAtUtc };
        var features = packages.SelectMany(x => x.Features).Where(x => x.IsEnabled).GroupBy(x => x.Feature.Code)
            .Select(g => g.OrderByDescending(x => x.Feature.DataType == FeatureDataType.Integer && long.TryParse(x.Value, out var limit) ? limit : 0).First()).ToArray();
        var trial = new RegistrationTrial { Id = subscription.Id, OrganizationId = org, CreatedAtUtc = registeredAtUtc,
            IncludedTeamUsers = packages.Max(x => x.IncludedTeamUsers), PaidTeamUserLimit = packages.Max(x => x.PaidTeamUserLimit),
            TeamUserMonthlyPriceIrt = anchor.TeamUserMonthlyPriceIrt,
            CreditAmountUsd = settings.CreditAmountUsd,
            AiPricePerMinuteUsd = anchor.AiPricePerMinuteUsd, RetentionDays = packages.Max(x => x.InterviewRetentionDays),
            Entitlements = features.Select(x => new RegistrationTrialEntitlement { TrialId = subscription.Id, Code = x.Feature.Code,
                Name = x.Feature.Name, DataType = x.Feature.DataType, Value = x.Value }).ToList() };
        var wallet = new OrganizationAiWallet { OrganizationId = org, Balance = settings.CreditAmountUsd, CreatedAtUtc = registeredAtUtc, UpdatedAtUtc = registeredAtUtc };
        db.OrganizationSubscriptions.Add(subscription); db.RegistrationTrials.Add(trial); db.OrganizationAiWallets.Add(wallet);
        db.AiCreditTransactions.Add(new() { OrganizationId = org, WalletId = wallet.Id, Type = CreditTransactionType.PromotionalCredit,
            Amount = settings.CreditAmountUsd, BalanceBefore = 0, BalanceAfter = settings.CreditAmountUsd, ReferenceType = "RegistrationTrial", ReferenceId = trial.Id,
            Description = "Registration trial credit", CreatedAtUtc = registeredAtUtc });
    }
}
