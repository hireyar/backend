using HireYar.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Billing;

internal static class InterviewTariffReader
{
    public static async Task<decimal> GetAsync(IApplicationDbContext db, string interviewType, CancellationToken ct)
    {
        var rate = await db.InterviewTariffs.AsNoTracking().Where(x => x.InterviewType == interviewType)
            .Select(x => (decimal?)x.RatePerMinuteUsd).SingleOrDefaultAsync(ct);
        return rate is > 0 and <= 1000 ? rate.Value
            : throw new AppException(503, "interview_tariff_unavailable", "The interview tariff is not configured.");
    }
}
