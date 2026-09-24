using HireYar.Application.Common;
using HireYar.Application.Hiring;
using HireYar.Infrastructure.Auth;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Hiring;

public sealed class InterviewEmailSender(IEmailSender sender, IOptions<SecurityOptions> options) : IInterviewEmail
{
    public Task SendAsync(InterviewEmail m, CancellationToken ct)
    {
        var link = options.Value.FrontendUrl.TrimEnd('/') + "/interview#token=" + Uri.EscapeDataString(m.Token);
        var body = $"دعوت به مصاحبه سازمان {m.OrganizationName}\nموقعیت: {m.JobTitle}\nمدت: {m.DurationMinutes} دقیقه\nشروع مجاز (UTC): {m.AvailableFromUtc:yyyy-MM-dd HH:mm}\nمهلت (UTC): {m.ExpiresAtUtc:yyyy-MM-dd HH:mm}\n{link}\nاین لینک اختصاصی است؛ آن را در اختیار دیگران نگذارید.";
        return sender.SendAsync(m.Recipient, "دعوت به مصاحبه", body, ct);
    }
}
