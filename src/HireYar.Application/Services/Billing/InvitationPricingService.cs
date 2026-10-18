using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Billing;

public sealed record InvitationPriceDto(Guid TemplateId, string Currency, decimal RatePerMinuteRial,
    decimal MaximumReservedRial, bool HasSufficientCredit, AiDisplayRateDto Rate);

public sealed class InvitationPricingService(IApplicationDbContext db, ISubscriptionEntitlementService entitlements,
    WalletService wallet, IAiDisplayRateProvider rates)
{
    public async Task<InvitationPriceDto> QuoteAsync(Guid org, Guid templateId, CancellationToken ct, Guid? packageId = null)
    {
        var template = await db.InterviewTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org &&
            x.Id == templateId && x.DeletedAtUtc == null && !x.IsArchived, ct) ?? throw AppException.NotFound();
        var capability = template.InterviewType == "specialized" ? "technical_interview" : "hr_interview";
        string[] required = template.UseTopicQuestions
            ? [capability, "ai_interview", "advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"]
            : [capability, "ai_interview"];
        decimal ratePerMinuteUsd;
        if (packageId is { } selectedId)
        {
            // Preview only: a selectable catalog package grants no subscription or capabilities.
            var package = await db.PricingCatalog.AsNoTracking().Include(x => x.Features).ThenInclude(x => x.Feature)
                .SingleOrDefaultAsync(x => x.Id == selectedId && x.IsActive && x.IsPublic && x.Periods.Any(p => p.IsActive), ct)
                ?? throw AppException.NotFound();
            if (required.Any(code => package.Features.All(x => !x.IsEnabled || x.Feature.Code != code)))
                throw new AppException(403, "package_feature_required", "The selected package does not include the required capability.");
            ratePerMinuteUsd = package.AiPricePerMinuteUsd;
        }
        else
        {
            var subscription = await entitlements.RequireAsync(org, required, ct);
            // Preserve the active paid/trial contract snapshot even if catalog prices change.
            ratePerMinuteUsd = subscription.AiRatePerMinuteUsd;
        }
        var quote = await rates.GetAsync(ct);
        var balance = await wallet.GetAsync(org, ct);
        var perMinute = decimal.Round(ratePerMinuteUsd * quote.RialPerUsdt, 6, MidpointRounding.ToEven);
        var maximum = BillingMath.Cost(template.DurationMinutes * 60, perMinute);
        // Preserve the shared refresh deadline, including last-known-rate fallback metadata.
        return new(templateId, "IRR", perMinute, maximum, balance.AvailableCredit >= maximum, quote);
    }
}
