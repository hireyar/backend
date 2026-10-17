using HireYar.Application.Common;
using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Billing;

public sealed record InvitationPriceDto(Guid TemplateId, string Currency, decimal RatePerMinuteRial,
    decimal MaximumReservedRial, bool HasSufficientCredit, AiDisplayRateDto Rate);

public sealed class InvitationPricingService(IApplicationDbContext db, ISubscriptionEntitlementService entitlements,
    WalletService wallet, IAiDisplayRateProvider rates)
{
    public async Task<InvitationPriceDto> QuoteAsync(Guid org, Guid templateId, CancellationToken ct)
    {
        var template = await db.InterviewTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org &&
            x.Id == templateId && x.DeletedAtUtc == null && !x.IsArchived, ct) ?? throw AppException.NotFound();
        var capability = template.InterviewType == "specialized" ? "technical_interview" : "hr_interview";
        var subscription = await entitlements.RequireAsync(org, template.UseTopicQuestions
            ? [capability, "ai_interview", "advanced_ai_workflow", "adaptive_questions", "adaptive_difficulty"]
            : [capability, "ai_interview"], ct);
        var quote = await rates.GetAsync(ct);
        var balance = await wallet.GetAsync(org, ct);
        var perMinute = decimal.Round(subscription.AiRatePerMinuteUsd * quote.RialPerUsdt, 6, MidpointRounding.ToEven);
        var maximum = BillingMath.Cost(template.DurationMinutes * 60, perMinute);
        // UI countdown is one minute; invitation creation independently obtains a current server quote.
        return new(templateId, "IRR", perMinute, maximum, balance.AvailableCredit >= maximum,
            quote with { RefreshAfterSeconds = 60, ExpiresAtUtc = quote.FetchedAtUtc.AddMinutes(1) });
    }
}
