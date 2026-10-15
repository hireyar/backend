using System.Net;
using System.Text;
using HireYar.Application.Interviews;

namespace HireYar.Api.Controllers;

public static class SessionReportPage
{
    public static string Render(SessionReportDocument report)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "ثبت نشده");
        var page = new StringBuilder("<!doctype html><html lang='fa' dir='rtl'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>گزارش مصاحبه</title><style>body{font:15px/1.9 Tahoma,sans-serif;background:#07111e;color:#d7e2ee;margin:0}main{max-width:1100px;margin:auto;padding:24px}header,section,article{background:#0d1c2e;border:1px solid #27405b;border-radius:14px;padding:18px;margin:14px 0}h1,h2{color:#5ee4b3}h2{font-size:19px}h3{font-size:16px}small,.pending{color:#a9b9cb}pre{white-space:pre-wrap;font:inherit}.stats{display:flex;gap:20px;flex-wrap:wrap}.badge{border:1px solid #376183;border-radius:12px;padding:3px 10px}.answer{border-right:3px solid #10b981;padding:12px;background:#0a1727}@media print{body{background:white;color:#111}header,section,article{background:white;color:#111;break-inside:avoid}h1,h2{color:#126349}.pending,small{color:#444}}</style><main>");
        page.Append("<header><small>HireYar — گزارش پایه مصاحبه</small><h1>").Append(E(report.Identity.CandidateName)).Append("</h1><p>")
            .Append(E(report.Identity.PositionTitle)).Append("</p><p>نوع: ").Append(report.Identity.InterviewType == "general" ? "عمومی" : "تخصصی")
            .Append(" — وضعیت جلسه: ").Append(E(report.Identity.InterviewStatus)).Append("</p><small>نسخه: ").Append(E(report.SchemaVersion)).Append("</small></header>");
        foreach (var section in report.Sections.Where(s => s.State != "NotApplicable")) {
            page.Append("<section data-section='").Append(E(section.Code)).Append("'><h2>").Append(E(section.Title)).Append("</h2>");
            if (section.Code == "identity") {
                page.Append("<p>شروع (UTC): ").Append(E(report.Identity.StartedAtUtc?.ToString("u"))).Append("</p><p>پایان (UTC): ")
                    .Append(E(report.Identity.CompletedAtUtc?.ToString("u"))).Append("</p>");
            } else if (section.Code == "statistics") {
                page.Append("<div class='stats'><span>سؤال پرسیده‌شده: ").Append(report.Statistics.AskedQuestions)
                    .Append("</span><span>پاسخ پردازش‌شده: ").Append(report.Statistics.ProcessedAnswers)
                    .Append("</span><span>بی‌پاسخ: ").Append(report.Statistics.UnansweredQuestions).Append("</span></div>");
            } else if (section.Code == "questions") {
                foreach (var q in report.Questions) {
                    page.Append("<article><small>سؤال ").Append(q.Number).Append(" — ").Append(E(q.Topic)).Append(" — سطح ").Append(q.Difficulty)
                        .Append("</small><h3>").Append(E(q.Text)).Append("</h3>");
                    foreach (var turn in q.Clarifications) page.Append("<p>تعامل: ").Append(E(turn.Transcript)).Append("</p><p>").Append(E(turn.AssistantMessage)).Append("</p>");
                    page.Append("<div class='answer'>").Append(q.Answer is null ? "این سؤال پاسخ نهایی ندارد." : E(q.Answer.Transcript))
                        .Append("</div><small>وضعیت پاسخ: ").Append(E(q.AnswerState)).Append(" — ارزیابی: هنوز انجام نشده</small></article>");
                }
                if (report.Questions.Count == 0) page.Append("<p>هنوز سؤالی پرسیده نشده است.</p>");
            } else if (section.Code == "signals") {
                page.Append("<p>رضایت: ").Append(E(report.Signals.ConsentState)).Append(" — رسانه: ").Append(E(report.Media.State)).Append("</p>");
                foreach (var signal in report.Signals.Events) page.Append("<p>").Append(E(signal.Kind)).Append(" — ").Append(E(signal.OccurredAtUtc?.ToString("u"))).Append("</p>");
                page.Append("<small>").Append(E(section.Message)).Append("</small>");
            } else page.Append("<p class='pending'>").Append(E(section.Message)).Append("</p>");
            page.Append("</section>");
        }
        return page.Append("<footer>این نسخه اطلاعات قطعی جلسه را نمایش می‌دهد. تحلیل‌ها هنوز ساخته نشده‌اند؛ تصمیم نهایی با کارشناس است.</footer></main></html>").ToString();
    }
}
