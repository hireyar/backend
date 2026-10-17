using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using HireYar.Application.Interviews;

namespace HireYar.Api.Controllers;

public static class SessionReportPage
{
    // The only executable content is fixed application code; report text is always HTML encoded.
    public const string PrintScript = "document.getElementById('print-report').addEventListener('click',()=>window.print());";
    public static string ContentSecurityPolicy => $"default-src 'none'; style-src 'unsafe-inline'; script-src 'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(PrintScript)))}'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private static string E(string? value) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "ثبت نشده" : value);
    private static string RawNumber(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string N(decimal? value) => value is null ? "نامعلوم" : Persian(RawNumber(value.Value));
    private static string Persian(string value) => string.Concat(value.Select(c => c is >= '0' and <= '9' ? (char)('۰' + c - '0') : c));
    private static string Percent(decimal? value) => value is null ? "نامعلوم" : N(value) + "٪";
    private static string Level(int value) => value <= 2 ? "ساده" : value == 3 ? "متوسط" : "سخت";
    private static string State(string value) => value switch
    {
        "Available" or "Succeeded" => "آماده", "Partial" => "شواهد ناقص", "InsufficientEvidence" => "شواهد ناکافی",
        "Queued" => "در صف ارزیابی", "Processing" => "در حال ارزیابی", "Failed" => "ارزیابی ناموفق",
        "RecoveryPending" => "نیازمند بازیابی", "NotReady" => "جلسه کامل نشده", "NotApplicable" => "بدون ارزیابی",
        "Missing" => "ضبط دریافت نشده", "Expired" => "دسترسی رسانه پایان یافته",
        "UploadedUnverified" => "دریافت‌شده؛ تأییدنشده", "Accepted" => "رضایت ثبت شده", "Revoked" => "رضایت لغو شده",
        "advance" => "ادامه فرآیند", "reject" => "رد", "review" => "بررسی توسط کارشناس", _ => "نامعلوم"
    };

    public static string Render(SessionReportDocument report)
    {
        var general = report.Identity.InterviewType == "general";
        var profile = general ? report.GeneralProfile : report.SpecializedProfile;
        var color = general ? "#38bdf8" : "#10b981";
        var p = new StringBuilder("<!DOCTYPE html><html lang='fa' dir='rtl'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>گزارش ارزیابی مصاحبه ـ HireYar</title><style>");
        p.Append(SessionReportStyles.Css).Append("""
            details.q>summary{list-style:none}details.q>summary::-webkit-details-marker{display:none}details.q[open] .q-body{display:block}details.q[open] .chev{transform:rotate(180deg)}
            .answer,.fb,.chip,.fl{white-space:pre-wrap;overflow-wrap:anywhere}.btn:disabled,.rtab:disabled{cursor:default;opacity:.55}.radar-wrap svg{max-width:100%;height:auto}.missing{color:#8ea9c4;font-size:12px}.legend .pc{width:auto;min-width:45px}.rec .verdict{display:flex;gap:10px;align-items:center;margin-bottom:12px}.rtab.on:disabled{opacity:1}.score-pill{direction:ltr;unicode-bidi:isolate}.q-body blockquote{margin:8px 0;border-right:2px solid #27486b;padding:4px 12px;color:#8ea9c4}
            @media print{body{padding:0;print-color-adjust:exact;-webkit-print-color-adjust:exact}.topbar{position:static}.tb-actions,.rec-actions,.rtabs,.q-tools{display:none}.wrap{padding:12px;max-width:none}.q-body{display:block!important}.q,.sw-box,.kpi{break-inside:avoid}.card{break-inside:auto}.hero,.kpis,.grid2,.charts,.rec{grid-template-columns:1fr 1fr}.hero{grid-template-columns:1.25fr .9fr 1fr}.report{display:block}.q-head{cursor:default}}
            </style></head><body><div class='topbar' data-section='navigation'><div style='display:flex;align-items:center;gap:16px'><div class='brand'>Hire<span>Yar</span></div><div class='crumb'>مصاحبه‌ها / گزارش ارزیابی /
            """).Append(E(report.Identity.CandidateName)).Append("</div></div><div class='tb-actions'><button class='btn' disabled title='مسیر پنل Angular در این ریپو موجود نیست'>‹ همه مصاحبه‌ها</button><button class='btn' disabled title='پخش خصوصی نیازمند پنل احرازشده است'>▶ دیدن ویدیو</button><button class='btn btn-p' id='print-report'>⬇ دانلود PDF گزارش</button></div></div><main class='wrap'><div class='rtabs'>");
        p.Append("<button class='rtab ").Append(general ? "" : "on").Append("' disabled>● گزارش مصاحبه تخصصی</button><button class='rtab ").Append(general ? "on" : "").Append("' disabled>● گزارش مصاحبه عمومی</button></div><div class='report on' id='rep-").Append(general ? "gen" : "spec").Append("'><div class='hero'><div class='card' data-section='identity'><h3>کارجو</h3><div class='cand'><div class='ava'>");
        p.Append(E(string.IsNullOrEmpty(report.Identity.CandidateName) ? null : StringInfo.GetNextTextElement(report.Identity.CandidateName))).Append("</div><div><div class='nm'>").Append(E(report.Identity.CandidateName)).Append("</div><div class='ps'>").Append(E(report.Identity.PositionTitle)).Append("</div></div></div><div class='badges'><span class='bdg ").Append(general ? "b" : "p").Append("'>").Append(general ? "مصاحبه عمومی HR" : "مصاحبه تخصصی").Append("</span><span class='bdg g'>").Append(N(report.Statistics.AskedQuestions)).Append(" سوال پرسیده‌شده</span><span class='bdg'>");
        if (report.Identity.StartedAtUtc is { } date)
        {
            var calendar = new PersianCalendar();
            var utc = date.UtcDateTime;
            p.Append(Persian($"{calendar.GetYear(utc):0000}/{calendar.GetMonth(utc):00}/{calendar.GetDayOfMonth(utc):00} ـ {utc:HH:mm}")).Append(" UTC");
        }
        else p.Append("زمان شروع ثبت نشده");
        p.Append("</span><span class='bdg'>مدت: ").Append(N(report.Identity.ElapsedSeconds is { } seconds ? Math.Round(seconds / 60m, 1) : null))            .Append(" دقیقه</span></div>");
        if(report.Narrative?.Experience is { Statements.Count: >0 } experience) {
            p.Append("<div class='missing'>سابقه اعلام‌شده کارجو:</div>");
            foreach(var statement in experience.Statements) p.Append("<p class='missing'>").Append(E(statement.Text)).Append("</p>");
        }
        p.Append("</div><div class='card donut-box' data-section='score'>");
        Ring(report.OverallScore.Value, true, "امتیاز کل", color);
        p.Append("<div class='donut-label'>").Append(E(State(report.OverallScore.State))).Append("</div><div class='donut-sub'>").Append(general ? "میانگین ارزیابی سوالات عمومی پاسخ‌داده‌شده" : "میانگین وزنی ارزیابی سوالات پرسیده‌شده").Append("</div></div><div class='card fit' data-section='positionFit'><h3>").Append(general ? "ارزیابی کلی" : "میزان تناسب با موقعیت").Append("</h3><div class='verdict'><div class='ic'>").Append(report.Coverage.IsProvisional ? "◌" : "●").Append("</div><div><div class='t'>").Append(report.Coverage.IsProvisional ? "شواهد ناقص ـ نیازمند بررسی انسانی" : "در انتظار بررسی انسانی").Append("</div><div class='d'>اطمینان ارزیابی: ").Append(Percent(report.OverallScore.Confidence * 100)).Append("</div></div></div>");
        if (general)
            foreach (var code in new[] { "communication", "motivation", "work_alignment" })
            {
                var metric = profile.Metrics.SingleOrDefault(m => m.Code == code);
                Bar(metric?.Title ?? (code == "communication" ? "ارتباطات و بیان" : code == "motivation" ? "انگیزه شغلی" : "همسویی با نقش شغلی"), metric?.Value, color, true);
            }
        else
        {
            Bar("تناسب کلی با موقعیت", report.PositionFit.Value, color, true);
            p.Append("<div class='missing'>پوشش شواهد نقش: ").Append(Percent(report.PositionFit.Coverage*100)).Append(" ـ ").Append(E(State(report.PositionFit.State))).Append("</div>");
            Bar("عمق دانش فنی", profile.Metrics.SingleOrDefault(m => m.Code == "knowledge_depth")?.Value, color, true);
            p.Append("<div class='row'><span>سطح پاسخ‌ها نسبت به انتظار</span></div><div class='missing' title='مقایسه دشواری سؤال دارای پاسخ ارزیابی‌شده با سطح هدف نقش؛ این شمارش نمره کیفیت نیست'>");
            if(report.ExpectedLevel is { Answers.Count: >0 } expected)
                p.Append("پایین‌تر: ").Append(N(expected.BelowTargetAnswers)).Append(" ـ هم‌سطح: ").Append(N(expected.AtTargetAnswers)).Append(" ـ بالاتر: ").Append(N(expected.AboveTargetAnswers));
            else p.Append("شواهد کافی نیست");
            p.Append("</div>");
        }
        p.Append("</div></div><div class='kpis' data-section='statistics'>");
        Kpi("سوالات پرسیده‌شده", N(report.Statistics.AskedQuestions) + " از " + N(report.Statistics.BankQuestions), general ? "بانک سوالات عمومی" : "موضوع‌های پرسیده‌شده: " + N(profile.Axes.Count));
        var scores = report.Questions.Where(q => q.Assessment.State == "Succeeded" && q.Assessment.Value is not null).Select(q => q.Assessment.Value!.Value).ToArray();
        Kpi(general ? "میانگین امتیاز" : "میانگین امتیاز پاسخ", scores.Length == 0 ? "نامعلوم" : N(scores.Average() / 20m) + " / ۵", "پاسخ نمره‌دار: " + N(scores.Length));
        if (general) Kpi("ویدیو و صدا", State(report.Media.State), "کامل‌بودن ضبط تأیید نشده است");
        else Kpi("سطح دشواری طی‌شده", report.Statistics.AskedDifficultyLevels.Count == 0 ? "ثبت نشده" : string.Join("، ", report.Statistics.AskedDifficultyLevels.Select(Level).Distinct()), N(report.Questions.Count(q => q.Difficulty >= 4 && q.Answer is not null)) + " پاسخ در سطح سخت");
        Kpi(general ? "مدت پاسخ‌ها" : "زمان پاسخ‌گویی میانگین", N(report.Statistics.AverageAnswerSeconds) + " ثانیه", "از مدت‌های ثبت‌شده");
        p.Append("</div><div class='grid2'><div class='card ai-sum' data-section='summary'><h3><span class='ic'>✦</span> جمع‌بندی هوش مصنوعی <span class='tag'>از ارزیابی پاسخ‌های واقعی</span></h3><p>").Append(E(report.Narrative?.Summary?.Text ?? report.Summary.Text)).Append("</p><div class='conf'>پوشش ارزیابی: ").Append(Percent(report.Coverage.Ratio * 100)).Append(report.Narrative?.Summary is null ? " ـ جمع‌بندی محاسباتی" : " ـ روایت AI با شاهد").Append("</div></div><div class='sw' data-section='strengthsAndImprovements'>");
        Findings(report.Strengths, "▲ نقاط قوت", "up");
        Findings(report.Improvements, "▼ نقاط ضعف و ریسک", "down");
        p.Append("</div></div><div class='charts' data-section='").Append(general ? "generalProfile" : "specializedProfile").Append("'><div class='card'><h3>").Append(general ? "شکاف امتیاز ـ توزیع پاسخ‌ها" : "پروفایل مهارتی ـ موضوعات پرسیده‌شده").Append("</h3>");
        if (general) Distribution(); else Radar();
        p.Append("</div><div class='card'><h3>").Append(general ? "شاخص‌های منابع انسانی" : "چهارچوب ارزیابی ـ نمره هر حوزه").Append("</h3>");
        if (!general)
        {
            p.Append("<div class='pies'>");
            foreach (var metric in profile.Metrics) { p.Append("<div class='pie'>"); Ring(metric.Value, false, metric.Title, color); p.Append("</div>"); }
            p.Append("</div><div class='legend' style='margin-top:16px'>");
            foreach (var axis in profile.Axes) Bar(axis.Topic, axis.Value, color);
            p.Append("</div>");
        }
        else
        {
            p.Append("<div class='legend'>");
            foreach (var metric in profile.Metrics) Bar(metric.Title, metric.Value, color);
            p.Append("</div>");
        }
        p.Append("</div></div><div class='qsec' data-section='questions'><div class='qsec-head'><h2>").Append(general ? "ارزیابی سوالات پاسخ‌داده‌شده" : "ارزیابی تک‌تک سوالات پرسیده‌شده").Append("</h2><div class='hint'>سوال‌های واقعی مصاحبه ـ همراه با متن پاسخ و دلیل نمره</div></div>");
        foreach (var q in report.Questions)
        {
            p.Append("<details class='q' id='q-").Append(q.QuestionId).Append("'").Append(q == report.Questions.First() ? " open" : "").Append("><summary class='q-head'><div><div class='q-meta'><span class='q-no'>سوال ").Append(N(q.Number)).Append("</span><span>● ").Append(E(q.Topic)).Append("</span>");
            if (!general) p.Append("<span class='lv-tag lv-").Append(q.Difficulty <= 2 ? "e" : q.Difficulty == 3 ? "m" : "h").Append("'>").Append(Level(q.Difficulty)).Append("</span>");
            p.Append("</div><div class='q-text'>").Append(E(q.Text)).Append("</div></div><div class='q-score'><span class='score-pill s").Append(q.Assessment.Value is { } scoreValue ? Math.Clamp((int)Math.Ceiling(scoreValue / 20m), 1, 5) : 3).Append("'>").Append(q.Assessment.Value is null ? "نامعلوم" : N(q.Assessment.Value / 20m) + " / ۵").Append("</span><span class='chev'>▾</span></div></summary><div class='q-body'><div class='lbl'>پاسخ کارجو</div><div class='answer'>").Append(q.Answer is null ? "این سؤال پاسخ نهایی ندارد." : E(q.Answer.Transcript)).Append("</div><div class='lbl'>ارزیابی AI</div><div class='fb'>").Append(E(State(q.Assessment.State))).Append(" ـ ").Append(E(q.Assessment.Explanation));
            foreach (var e in q.Assessment.Evidence) p.Append("<blockquote>").Append(E(e.Quote)).Append("</blockquote>");
            p.Append("</div><div class='q-tools'><button class='btn' disabled title='پخش صدا از پنل احرازشده انجام می‌شود'>▶ شنیدن پاسخ</button>");
            var offset = report.Media.Recording?.Timeline.FirstOrDefault(t => t.AnswerId == q.Answer?.AnswerId);
            if (offset is not null) p.Append("<span class='ts'>").Append(N(offset.StartSeconds)).Append(" – ").Append(N(offset.EndSeconds)).Append(" s</span>");
            p.Append("<span class='bdg'>اطمینان نمره: ").Append(Percent(q.Assessment.Confidence * 100)).Append("</span></div></div></details>");
        }
        if (report.Questions.Count == 0) p.Append("<p class='missing'>هنوز سؤالی پرسیده نشده است.</p>");
        p.Append("</div><div class='rec'><div class='card rec-main' data-section='humanReview'><h3>پیشنهاد نهایی</h3><div class='verdict'><div class='ic'>🎯</div><div><div class='t'>").Append(report.HumanReview.Latest is { } latest ? E(State(latest.Decision)) : "تصمیم انسانی ثبت نشده است").Append("</div><div class='d'>امتیاز ").Append(Percent(report.OverallScore.Value)).Append("</div></div></div><p class='missing'>").Append(E(report.HumanReview.Message)).Append("</p>");
        if(report.Narrative?.NextStep is { } next) p.Append("<p class='fb'>").Append(E(next.Text)).Append("</p>");
        if(report.Narrative is { FollowUpQuestions.Count: >0 } narrative) {
            p.Append("<div class='lbl'>سؤال‌های پیشنهادی مرحله بعد</div>");
            foreach(var followUp in narrative.FollowUpQuestions) p.Append("<p class='fb'>").Append(E(followUp.Text)).Append("<br><small>").Append(E(followUp.Reason)).Append("</small></p>");
        }
        if (report.HumanReview.Latest is { } review) p.Append("<p>").Append(E(review.Note)).Append("</p>");
        p.Append("<div class='rec-actions'><button class='btn btn-p' disabled>✓ ").Append(general ? "عبور از غربالگری اولیه" : "پذیرش و دعوت به مرحله بعد").Append("</button><button class='btn' disabled>بررسی توسط کارشناس</button><button class='btn' disabled>↩ رد</button></div></div><div class='card flags' data-section='signals'><h3>نشانه‌ها و هشدارها</h3><div class='fl'>رضایت ضبط: ").Append(E(State(report.Signals.ConsentState))).Append("</div><div class='fl'>ویدیو و صدا: ").Append(E(State(report.Media.State))).Append("</div>");
        foreach (var signal in report.Signals.Events) p.Append("<div class='fl'>").Append(E(Signal(signal.Kind))).Append(" ـ ").Append(E((signal.OccurredAtUtc ?? signal.RecordedAtUtc).ToString("u", CultureInfo.InvariantCulture))).Append("</div>");
        foreach (var gap in report.DataGaps) p.Append("<div class='fl'>").Append(E(gap.Message)).Append("</div>");
        return p.Append("</div></div></div></main><script type='module'>").Append(PrintScript).Append("</script></body></html>").ToString();

        void Kpi(string title, string value, string foot) => p.Append("<div class='kpi'><div class='l'>").Append(E(title)).Append("</div><div class='v'>").Append(value.Contains(" / ", StringComparison.Ordinal) ? "<bdi dir='ltr'>" + E(value) + "</bdi>" : E(value)).Append("</div><div class='f'>").Append(E(foot)).Append("</div></div>");
        void Ring(decimal? value, bool large, string title, string tint)
        {
            p.Append("<div class='").Append(large ? "donut" : "circle").Append("' style='background:");
            if (value is null) p.Append("#16293f");
            else p.Append("conic-gradient(").Append(tint).Append(" 0 ").Append(RawNumber(Math.Clamp(value.Value, 0, 100))).Append("%,#16293f 0 100%)");
            p.Append("'>");
            if (large) p.Append("<div class='val'><b>").Append(Percent(value)).Append("</b><span>").Append(E(title)).Append("</span></div>");
            else p.Append("<b>").Append(Percent(value)).Append("</b>");
            p.Append("</div>");
            if (!large) p.Append("<div class='cap'>").Append(E(title)).Append("</div>");
        }
        void Bar(string title, decimal? value, string tint, bool hero = false)
        {
            p.Append(hero ? "<div class='row'>" : "<div class='li'>").Append("<span class='nm'>").Append(E(title)).Append("</span>");
            if (hero) p.Append("<b>").Append(Percent(value)).Append("</b></div>");
            p.Append(hero ? "<div class='meter'>" : "<span class='bar'>");
            if (value is not null) p.Append("<i style='width:").Append(RawNumber(Math.Clamp(value.Value, 0, 100))).Append("%;background:").Append(tint).Append("'></i>");
            p.Append(hero ? "</div>" : "</span><span class='pc'>" + Percent(value) + "</span></div>");
        }
        void Findings(ReportNotes notes, string title, string css)
        {
            p.Append("<div class='sw-box ").Append(css).Append("'><h4>").Append(E(title)).Append("</h4><div class='chips'>");
            foreach (var item in notes.Items) p.Append("<span class='chip'>").Append(E(item)).Append("</span>");
            if (notes.Items.Count == 0) p.Append("<span class='missing'>مورد مستندی ثبت نشده است.</span>");
            p.Append("</div></div>");
        }
        void Distribution()
        {
            var d = profile.Distribution;
            var total = d.Strong + d.Satisfactory + d.NeedsImprovement;
            p.Append("<div class='pies'><div class='pie'>");
            if (total == 0) Ring(null, false, "پاسخ‌های نمره‌دار", color);
            else
            {
                var strong = RawNumber(100m * d.Strong / total);
                var good = RawNumber(100m * (d.Strong + d.Satisfactory) / total);
                p.Append("<div class='circle' style='background:conic-gradient(#10b981 0 ").Append(strong).Append("%,#fbbf24 ").Append(strong).Append("% ").Append(good).Append("%,#fb7185 ").Append(good).Append("% 100%)'><b>").Append(N(total)).Append("</b></div><div class='cap'>پاسخ‌های نمره‌دار</div>");
            }
            p.Append("</div><div class='pie'>"); Ring(total == 0 ? null : 100m * (d.Strong + d.Satisfactory) / total, false, "بالای آستانه", color);
            p.Append("</div><div class='pie'>"); Ring(profile.Metrics.SingleOrDefault(m => m.Code == "communication")?.Value, false, "کیفیت ارتباط", "#a78bfa");
            p.Append("</div></div><div class='legend' style='margin-top:14px'>");
            foreach (var row in new[] { ("پاسخ‌های قوی", d.Strong), ("پاسخ‌های در آستانه", d.Satisfactory), ("پاسخ‌های نیازمند بهبود", d.NeedsImprovement), ("بدون نمره", d.Unscored) })
                p.Append("<div class='li'><span class='nm'>").Append(row.Item1).Append("</span><span class='pc'>").Append(N(row.Item2)).Append("</span></div>");
            p.Append("</div>");
        }
        void Radar()
        {
            var axes = profile.Axes;
            if (axes.Count < 3) { p.Append("<p class='missing'>برای رسم رادار حداقل سه موضوع لازم است.</p><div class='legend'>"); foreach (var axis in axes) Bar(axis.Topic, axis.Value, color); p.Append("</div>"); return; }
            string Point(int i, decimal radius)
            {
                var angle = -Math.PI / 2 + 2 * Math.PI * i / axes.Count;
                return RawNumber(140 + (decimal)Math.Cos(angle) * radius) + "," + RawNumber(140 + (decimal)Math.Sin(angle) * radius);
            }
            p.Append("<div class='radar-wrap'><svg viewBox='0 0 280 280' width='270' height='270' role='img' aria-label='پروفایل مهارتی'>");
            foreach (var radius in new[] { 25, 50, 75, 100 }) p.Append("<polygon fill='none' stroke='#1a2f47' points='").Append(string.Join(" ", Enumerable.Range(0, axes.Count).Select(i => Point(i, radius)))).Append("'/>");
            if (axes.All(a => a.Value is not null)) p.Append("<polygon fill='rgba(16,185,129,.28)' stroke='#10b981' stroke-width='2' points='").Append(string.Join(" ", Enumerable.Range(0, axes.Count).Select(i => Point(i, Math.Clamp(axes[i].Value!.Value, 0, 100))))).Append("'/>");
            for (var i = 0; i < axes.Count; i++)
            {
                var location = Point(i, 118).Split(',');
                p.Append("<text x='").Append(location[0]).Append("' y='").Append(location[1]).Append("' fill='#8ea9c4' font-size='10' text-anchor='middle'>").Append(E(axes[i].Topic)).Append("</text>");
            }
            p.Append("</svg></div>");
            if (axes.Any(a => a.Value is null)) p.Append("<p class='missing'>رادار کامل رسم نشده؛ بعضی موضوع‌ها شواهد کافی ندارند.</p>");
        }
    }

    private static string Signal(string kind) => kind switch
    {
        "consent_accepted" => "رضایت ضبط ثبت شد", "consent_revoked" => "رضایت ضبط لغو شد",
        "microphone_disconnected" => "قطع میکروفون گزارش شد", "camera_disconnected" => "قطع دوربین گزارش شد",
        "page_hidden" => "خروج از صفحه گزارش شد", "page_visible" => "بازگشت به صفحه گزارش شد", _ => kind
    };
}
