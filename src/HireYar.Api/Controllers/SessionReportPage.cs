using System.Globalization;
using System.Net;
using System.Text;
using HireYar.Application.Interviews;

namespace HireYar.Api.Controllers;

public static class SessionReportPage
{
    public static string Render(SessionReportDocument report)
    {
        static string E(string? s)=>WebUtility.HtmlEncode(s??"ثبت نشده");
        static string N(decimal? n)=>n?.ToString("0.##",CultureInfo.InvariantCulture)??"نامعلوم";
        static string T(DateTimeOffset? t)=>t?.ToString("u",CultureInfo.InvariantCulture)??"ثبت نشده";
        static string Label(string s)=>s switch {
            "Available" or "Succeeded"=>"آماده", "Partial"=>"شواهد ناقص", "InsufficientEvidence"=>"شواهد ناکافی",
            "NotReady"=>"جلسه هنوز کامل نشده", "Queued"=>"در صف ارزیابی پاسخ", "Processing"=>"در حال ارزیابی پاسخ", "RecoveryPending"=>"نیازمند بازیابی",
            "Unavailable"=>"داده قابل اتکا در دسترس نیست", "Failed"=>"ارزیابی ناموفق", "NoFindings"=>"موردی با این معیار یافت نشد",
            "AwaitingReview"=>"در انتظار تصمیم کارشناس", "Reviewed"=>"بررسی شده", "ReviewOutdated"=>"نیازمند بازبینی نسخه جدید", "UnversionedReview"=>"تصمیم قبلی بدون نسخه گزارش",
            "advance"=>"ادامه فرآیند", "review"=>"بررسی بیشتر", "reject"=>"عدم ادامه فرآیند", "Missing"=>"ضبط دریافت نشده", "Expired"=>"مدت دسترسی رسانه پایان یافته",
            "UploadedUnverified"=>"رسانه دریافت‌شده و تأییدنشده", "Unknown"=>"نامشخص", "Accepted"=>"رضایت ثبت شده", "Revoked"=>"رضایت لغو شده", _=>s };
        var p=new StringBuilder("""
            <!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>گزارش جامع مصاحبه</title><style>
            *{box-sizing:border-box}body{margin:0;font:14px/1.95 Tahoma,sans-serif;background:#07111e;color:#d7e2ee}main{max-width:1160px;margin:auto;padding:24px}
            header,section{padding:24px;margin:18px 0;background:#0d1c2e;border:1px solid #27405b;border-radius:16px}article{padding:16px;margin:12px 0;background:#0a1727;border:1px solid #27405b;border-radius:10px}
            h1,h2{color:#5ee4b3}h2{font-size:21px}h3{font-size:16px}a{color:#6bdac3}nav{display:flex;gap:12px;flex-wrap:wrap}small,.muted{color:#a9b9cb}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:16px}
            .score{font-size:46px;font-weight:bold;color:#5ee4b3}.answer{border-right:3px solid #10b981;padding:12px;white-space:pre-wrap}blockquote{border-right:2px solid #4b7994;padding:8px 14px;margin:8px 0;white-space:pre-wrap}
            table{width:100%;border-collapse:collapse}td,th{border-bottom:1px solid #27405b;text-align:right;padding:9px;vertical-align:top}meter{width:100%;height:15px;accent-color:#10b981}.status{padding:3px 9px;background:#1c3048;border-radius:8px}
            .formula{direction:ltr;text-align:left;overflow-wrap:anywhere;font-size:11px}.radar{width:100%;max-width:440px;height:auto}.radar text{fill:currentColor;font:10px Tahoma,sans-serif}.warning{color:#f9cb75}
            @media(max-width:600px){main{padding:10px}section,header{padding:14px}table{font-size:11px}}
            @media print{body{background:white;color:#111}main{max-width:none;padding:0}header,section,article{background:white;color:#111;border-color:#bbb}article,tr,blockquote,svg{break-inside:avoid}nav{display:none}h1,h2,.score{color:#126349}small,.muted{color:#444}.warning{color:#725000}a{color:#126349}.status{background:#eee}section{break-before:auto}}
            </style></head><body><main>
            """);
        p.Append("<header><small>HireYar — گزارش جامع مصاحبه</small><h1>").Append(E(report.Identity.CandidateName)).Append("</h1><p>").Append(E(report.Identity.PositionTitle))
            .Append(" — ").Append(report.Identity.InterviewType=="general"?"مصاحبه عمومی":"مصاحبه تخصصی").Append("</p><p>").Append(E(report.Processing.Message))
            .Append("</p><small>نسخه داده: ").Append(E(report.SchemaVersion)).Append(" — فرمول‌ها و شواهد قابل بررسی‌اند؛ تصمیم نهایی با کارشناس است.</small></header><nav>");
        foreach(var s in report.Sections.Where(s=>s.State!="NotApplicable")) p.Append("<a href='#").Append(E(s.Code)).Append("'>").Append(E(s.Title)).Append("</a>");
        p.Append("</nav>");
        void Meter(decimal? value) {if(value is null)p.Append("<span class='muted'>نامعلوم — شواهد کافی نیست</span>");else p.Append("<meter min='0' max='100' value='").Append(N(value)).Append("'>").Append(N(value)).Append("</meter><b>").Append(N(value)).Append(" از ۱۰۰</b>");}
        void Evidence(IEnumerable<AnswerAssessmentEvidence> evidence) {foreach(var e in evidence)p.Append("<blockquote>").Append(E(e.Quote)).Append("</blockquote>");}
        void Findings(ReportNotes notes,string title) {p.Append("<h3>").Append(E(title)).Append("</h3>");if(notes.Findings is not {Count:>0})p.Append("<p class='muted'>مورد مستندی با آستانه‌های فعلی ثبت نشده است.</p>");else foreach(var f in notes.Findings){p.Append("<article><a href='#q-").Append(f.QuestionId).Append("'>مشاهده پاسخ</a><p>").Append(E(f.Text)).Append("</p>");Evidence(f.Evidence);p.Append("</article>");}}
        void Profile(ReportProfile profile) {
            p.Append("<p>").Append(E(profile.Message)).Append("</p>");
            if(profile.Axes.Count>=3) {
                var count=profile.Axes.Count;var known=profile.Axes.All(a=>a.Value is not null);
                string Point(int i,decimal value) {var angle=-Math.PI/2+2*Math.PI*i/count;return N((decimal)(180+Math.Cos(angle)*(double)value))+","+N((decimal)(170+Math.Sin(angle)*(double)value));}
                p.Append("<svg class='radar' viewBox='0 0 360 350' role='img' aria-label='پروفایل موضوعات؛ مقادیر نامعلوم صفر نیستند'>");
                foreach(var radius in new[]{40,80,120})p.Append("<polygon fill='none' stroke='#7290a6' opacity='.35' points='").Append(string.Join(" ",Enumerable.Range(0,count).Select(i=>Point(i,radius)))).Append("'/>");
                if(known)p.Append("<polygon fill='#10b981' fill-opacity='.2' stroke='#10b981' points='").Append(string.Join(" ",Enumerable.Range(0,count).Select(i=>Point(i,profile.Axes[i].Value!.Value*1.2m)))).Append("'/>");
                for(var i=0;i<count;i++){var point=Point(i,142).Split(',');p.Append("<text text-anchor='middle' x='").Append(point[0]).Append("' y='").Append(point[1]).Append("'>").Append(E(profile.Axes[i].Topic.Length>18?profile.Axes[i].Topic[..18]+"…":profile.Axes[i].Topic)).Append("</text>");}
                p.Append("</svg>");if(!known)p.Append("<p class='muted'>شکل رادار کامل رسم نشده؛ بعضی محور‌ها شواهد کافی ندارند.</p>");
            }
            foreach(var axis in profile.Axes){p.Append("<article><h3>").Append(E(axis.Topic)).Append("</h3>");Meter(axis.Value);p.Append("<p>سطوح پرسیده‌شده: ").Append(E(string.Join("، ",axis.DifficultyLevels))).Append(" — پوشش: ").Append(axis.ScoredQuestions).Append(" از ").Append(axis.AskedQuestions).Append("</p></article>");}
            p.Append("<h3>شاخص‌های ارزیابی</h3>");foreach(var metric in profile.Metrics){p.Append("<article><h3>").Append(E(metric.Title)).Append("</h3>");Meter(metric.Value);p.Append("<p>").Append(E(Label(metric.State))).Append(" — پاسخ مستند: ").Append(metric.ScoredAnswers).Append(" از ").Append(metric.ExpectedAnswers).Append("</p><small>").Append(E(metric.Message)).Append("</small>");Evidence(metric.Evidence);p.Append("</article>");}
        }
        foreach(var section in report.Sections.Where(s=>s.State!="NotApplicable")) {
            p.Append("<section id='").Append(E(section.Code)).Append("' data-section='").Append(E(section.Code)).Append("'><h2>").Append(E(section.Title)).Append("</h2><span class='status'>").Append(E(Label(section.State))).Append("</span>");
            switch(section.Code) {
                case "navigation":p.Append("<p>برای ذخیره PDF از دستور چاپ مرورگر استفاده کنید. رسانه خصوصی از پنل احراز هویت‌شده باز می‌شود.</p><p>وضعیت رسانه: ").Append(E(Label(report.Media.State))).Append("؛ قطعات دریافت‌شده: ").Append(report.Media.Segments.Count).Append("</p>");break;
                case "identity":p.Append("<p>وضعیت جلسه: ").Append(E(report.Identity.InterviewStatus)).Append("</p><p>ایجاد (UTC): ").Append(T(report.Identity.CreatedAtUtc)).Append("</p><p>شروع (UTC): ").Append(T(report.Identity.StartedAtUtc)).Append("</p><p>پایان (UTC): ").Append(T(report.Identity.CompletedAtUtc)).Append("</p><p>مدت جلسه: ").Append(report.Identity.ElapsedSeconds?.ToString()??"نامعلوم").Append(" ثانیه</p>");break;
                case "score":p.Append("<div class='score'>").Append(N(report.OverallScore.Value)).Append("</div><p>امتیاز مشاهده‌شده از ۱۰۰ — اطمینان مدل: ").Append(N(report.OverallScore.Confidence)).Append("</p><p>پاسخ نمره‌دار: ").Append(report.Coverage.ScoredQuestions).Append(" از ").Append(report.Coverage.AskedQuestions).Append(" — پوشش: ").Append(N(report.Coverage.Ratio*100)).Append("٪</p>");if(report.Coverage.IsProvisional)p.Append("<p class='warning'>پوشش شواهد ناقص است؛ نمره را نتیجه قطعی تلقی نکنید.</p>");break;
                case "statistics":var d=report.Identity.InterviewType=="general"?report.GeneralProfile.Distribution:report.SpecializedProfile.Distribution;p.Append("<div class='grid'><p>پرسیده‌شده: ").Append(report.Statistics.AskedQuestions).Append("</p><p>پاسخ دریافت‌شده: ").Append(report.Statistics.SubmittedAnswers).Append("</p><p>پردازش متن: ").Append(report.Statistics.ProcessedAnswers).Append("</p><p>بی‌پاسخ: ").Append(report.Statistics.UnansweredQuestions).Append("</p><p>تعامل‌ها: ").Append(report.Statistics.ClarificationTurns).Append("</p><p>متوسط مدت پاسخ: ").Append(N(report.Statistics.AverageAnswerSeconds)).Append(" ثانیه</p></div><p>قوی: ").Append(d.Strong).Append("؛ در آستانه: ").Append(d.Satisfactory).Append("؛ نیازمند بهبود: ").Append(d.NeedsImprovement).Append("؛ بدون نمره: ").Append(d.Unscored).Append("</p>");break;
                case "summary":p.Append("<p>").Append(E(report.Summary.Text)).Append("</p>");foreach(var g in report.DataGaps)p.Append("<p class='warning'>").Append(E(g.Message)).Append("</p>");break;
                case "strengthsAndImprovements":Findings(report.Strengths,"نقاط قوت مستند");Findings(report.Improvements,"زمینه‌های بهبود مستند");break;
                case "specializedProfile":Profile(report.SpecializedProfile);break;
                case "generalProfile":Profile(report.GeneralProfile);break;
                case "questions":foreach(var q in report.Questions){p.Append("<article id='q-").Append(q.QuestionId).Append("'><h3>سؤال ").Append(q.Number).Append(" — ").Append(E(q.Topic)).Append("</h3><small>سطح ").Append(q.Difficulty).Append("</small><p>").Append(E(q.Text)).Append("</p>");foreach(var t in q.Clarifications)p.Append("<p>تعامل: ").Append(E(t.Transcript)).Append("</p><p>").Append(E(t.AssistantMessage)).Append("</p>");p.Append("<div class='answer'>").Append(q.Answer is null?"این سؤال پاسخ نهایی ندارد.":E(q.Answer.Transcript)).Append("</div><p>ارزیابی: ").Append(E(Label(q.Assessment.State))).Append("</p>");if(q.Assessment.Value is not null){p.Append("<p>امتیاز پاسخ: ").Append(N(q.Assessment.Value)).Append(" از ۱۰۰</p>");}p.Append("<p>").Append(E(q.Assessment.Explanation)).Append("</p>");Evidence(q.Assessment.Evidence);if(q.Assessment.FailureCode is not null)p.Append("<small>کد وضعیت: ").Append(E(q.Assessment.FailureCode)).Append("</small>");p.Append("</article>");}if(report.Questions.Count==0)p.Append("<p>هنوز سؤالی پرسیده نشده است.</p>");break;
                case "signals":p.Append("<p>رضایت: ").Append(E(Label(report.Signals.ConsentState))).Append(" — دستگاه: ").Append(E(Label(report.Signals.DeviceState))).Append("</p><p>رسانه: ").Append(E(Label(report.Media.State))).Append("</p><small>").Append(E(section.Message)).Append("</small>");foreach(var e in report.Signals.Events)p.Append("<p>").Append(E(e.Kind)).Append(" — ").Append(T(e.OccurredAtUtc)).Append("</p>");break;
                case "humanReview":p.Append("<p>").Append(E(report.HumanReview.Message)).Append("</p>");foreach(var h in report.HumanReview.History.Reverse())p.Append("<article><h3>").Append(E(Label(h.Decision))).Append("</h3><p>").Append(E(h.Note)).Append("</p><small>").Append(T(h.RecordedAtUtc)).Append(" — ").Append(h.MatchesCurrentReport?"متصل به این نسخه":"نسخه قبلی یا تصمیم بدون نسخه").Append("</small></article>");break;
            }
            p.Append("</section>");
        }
        return p.Append("<footer><small>اثر انگشت گزارش: ").Append(E(report.Fingerprint)).Append("</small><p>این گزارش نتیجه بررسی شواهد مصاحبه است؛ داده ناکافی به معنی ناتوانی فرد نیست و تصمیم نهایی با کارشناس است.</p></footer></main></body></html>").ToString();
    }
}
