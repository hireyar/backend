using System.Text;
using System.Text.Encodings.Web;
using HireYar.Application.Interviews;

namespace HireYar.Api.Controllers;

public static class InterviewReportPrint
{
    public static string Render(InterviewReportDto report, InterviewReviewDto? review = null)
    {
        static string E(object? value) => HtmlEncoder.Default.Encode(value?.ToString() ?? "—");
        var html = new StringBuilder("<!doctype html><html lang=\"fa\" dir=\"rtl\"><meta charset=\"utf-8\"><title>گزارش مصاحبه</title><style>body{font-family:Tahoma,sans-serif;line-height:1.9;max-width:960px;margin:32px auto;padding:16px;color:#172c45}table{width:100%;border-collapse:collapse}th,td{padding:8px;border:1px solid #ccc;text-align:right}article{border-top:1px solid #ccc;margin-top:20px;break-inside:avoid}p{white-space:pre-wrap;overflow-wrap:anywhere}@media print{body{margin:0;max-width:none}table{font-size:11px}thead{display:table-header-group}}@page{size:A4;margin:18mm}</style><body><h1>گزارش ارزیابی مصاحبه</h1>");
        html.Append("<p>شناسه: ").Append(E(report.InterviewId)).Append(" · زمان UTC: ").Append(E(report.CreatedAtUtc.ToString("u"))).Append("</p>");
        html.Append("<p>").Append(E(report.Content.Summary)).Append("</p>");
        if (report.Content.Details is { } details)
        {
            html.Append("<p>نمره کل از ۱۰۰: ").Append(E(details.OverallScore)).Append(" · نیازمند بررسی انسانی: ").Append(details.RequiresHumanReview ? "بله" : "خیر").Append("</p>");
            html.Append("<p>امتیاز کل از میانگین نمره پاسخ‌های ارزیابی‌شده با وزن موضوعات محاسبه می‌شود. وزن پیش‌فرض هر موضوع یک است.</p>");
            html.Append("<p>پرسیده‌شده: ").Append(E(details.AskedCount)).Append(" از ").Append(E(details.BankCount))
                .Append(" · اطمینان تخمینی مدل: ").Append(E(details.Confidence)).Append("</p><table><thead><tr><th>موضوع</th><th>نمره از ۱۰۰</th><th>سطح پرسیده‌شده</th></tr></thead><tbody>");
            foreach (var topic in details.Topics)
                html.Append("<tr><td>").Append(E(topic.Topic)).Append("</td><td>").Append(E(topic.Score)).Append("</td><td>").Append(E(topic.ReachedDifficulty)).Append("</td></tr>");
            html.Append("</tbody></table>");
            html.Append("<h2>نقاط قوت</h2>");
            foreach (var strength in details.Strengths) html.Append("<p>").Append(E(strength)).Append("</p>");
            html.Append("<h2>شکاف‌های پاسخ</h2>");
            foreach (var gap in details.Gaps) html.Append("<p>").Append(E(gap)).Append("</p>");
            foreach (var question in details.Questions)
                html.Append("<article><h2>").Append(E(question.Text)).Append("</h2><p>نمره از ۵: ").Append(E(question.Score))
                    .Append("</p><p>").Append(E(question.Transcript)).Append("</p><p>").Append(E(question.Feedback)).Append("</p></article>");
        }
        foreach (var metric in report.Content.Metrics ?? [])
        {
            var label = metric.Code switch { "knowledge_depth" => "عمق دانش", "communication" => "ارتباطات و بیان", "job_alignment" => "تناسب مهارت با شرح شغل",
                "motivation" => "انگیزه شغلی", "teamwork" => "کار تیمی", "working_practices" => "روش‌های کاری مرتبط با شغل",
                "responsibility" => "مسئولیت‌پذیری", "conflict_management" => "مدیریت تعارض", "documentation" => "انضباط و مستندسازی", _ => metric.Code };
            html.Append("<article><h3>").Append(E(label)).Append(" · ").Append(metric.Score is null ? "شواهد ناکافی" : E(metric.Score * 20) + " / ۱۰۰")
                .Append("</h3><p>").Append(E(metric.Explanation)).Append("</p>");
            foreach (var citation in metric.Evidence) html.Append("<p>شاهد: «").Append(E(citation.Quote)).Append("»</p>");
            html.Append("</article>");
        }
        if (review is not null)
        {
            html.Append("<h2>رضایت و سابقه بررسی انسانی</h2><p>رضایت گزارش‌شده: ").Append(review.ConsentStatus switch { "accepted" => "تأیید شده", "revoked" => "پس گرفته شده", _ => "نامشخص" })
                .Append(" · ضبط کامل: ").Append(review.Recording is null ? "نامشخص" : "فایل آپلود شده؛ کامل‌بودن تأیید نشده").Append("</p>");
            foreach (var entry in review.Entries.Where(e => e.ActorUserId is not null))
                html.Append("<p>").Append(entry.Kind switch { "advance" => "مرحله بعد", "reject" => "رد", _ => "بررسی انسانی" }).Append(" · ")
                    .Append(E(entry.RecordedAtUtc.ToString("u"))).Append(" · ").Append(E(entry.Note)).Append("</p>");
            var disruptions = review.Entries.Count(e => e.Kind is "microphone_disconnected" or "camera_disconnected" or "page_hidden");
            html.Append("<p>رویدادهای اختلال/خروج گزارش‌شده توسط کلاینت: ").Append(disruptions).Append("؛ نبود رویداد، سلامت ضبط را اثبات نمی‌کند.</p>");
        }
        html.Append("<h2>محورهای مرحله بعد</h2>");
        foreach (var focus in report.Content.NextRoundFocus ?? []) html.Append("<p>").Append(E(focus)).Append("</p>");
        html.Append("<h2>محدودیت‌ها</h2>");
        foreach (var limitation in report.Content.Limitations) html.Append("<p>").Append(E(limitation)).Append("</p>");
        return html.Append("<p>تصمیم نهایی با بررسی‌کننده انسانی است. نسخه چاپی با Print / Save as PDF مرورگر قابل ذخیره است.</p></body></html>").ToString();
    }
}
