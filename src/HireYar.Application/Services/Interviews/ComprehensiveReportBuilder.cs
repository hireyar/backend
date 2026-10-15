using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public sealed record ComprehensiveReport(ReportScore Score, ReportText Summary, ReportNotes Strengths,
    ReportNotes Improvements, ReportCoverage Coverage, ReportPositionFit Fit, ReportProfile Specialized,
    ReportProfile General, ReportHumanReview Review, IReadOnlyList<ReportGap> Gaps, string Fingerprint);

public static class ComprehensiveReportBuilder
{
    private static decimal R(decimal n)=>Math.Round(n,2,MidpointRounding.AwayFromZero);
    private static bool Scored(ReportQuestion q)=>q.Assessment.State=="Succeeded" && q.Assessment.Value is >=0 and <=100 && q.Assessment.Confidence is >=0 and <=1 && q.Assessment.Evidence.Count>0;
    private static string State(int known,int expected,bool completed)=>!completed?"NotReady":known==0?"InsufficientEvidence":known==expected?"Available":"Partial";
    public static ComprehensiveReport Build(InterviewSession session, IReadOnlyList<ReportQuestion> questions,
        ReportPolicy policy, IReadOnlyList<InterviewReviewEntry> reviews, bool canReview)
    {
        var completed=session.Status==InterviewStatus.Completed;
        var general=session.RequiredCapabilityCode=="hr_interview";
        var snapshots=session.Questions.ToDictionary(q=>q.QuestionId);
        var scored=questions.Where(Scored).ToArray();
        var weights=policy.Weights.ToDictionary(w=>w.SkillId,w=>w.Weight);
        var coverage=new ReportCoverage(questions.Count,scored.Length,questions.Count(q=>q.Answer is null),
            questions.Count(q=>q.Assessment.State=="Failed"),questions.Count(q=>q.Assessment.IsPending),
            questions.Count==0?0:R((decimal)scored.Length/questions.Count),!completed || scored.Length!=questions.Count);
        var axes=questions.GroupBy(q=>(snapshots[q.QuestionId].SkillId,q.Topic)).Select(g=> {
            var known=g.Where(Scored).ToArray();
            return new ReportProfileAxis(g.Key.SkillId,g.Key.Topic,State(known.Length,g.Count(),completed),
                known.Length==0?null:R(known.Average(q=>q.Assessment.Value!.Value)),known.Length==0?null:R(known.Average(q=>q.Assessment.Confidence!.Value)),
                g.Count(),known.Length,g.Select(q=>q.Difficulty).Distinct().Order().ToArray(),g.Select(q=>q.QuestionId).ToArray(),known.SelectMany(q=>q.Assessment.Evidence).ToArray());
        }).ToArray();
        var skillScores=scored.GroupBy(q=>snapshots[q.QuestionId].SkillId).Select(g=>new {Id=g.Key,Score=g.Average(q=>q.Assessment.Value!.Value),Confidence=g.Average(q=>q.Assessment.Confidence!.Value),Weight=weights.GetValueOrDefault(g.Key,1)}).Where(s=>s.Weight>0).ToArray();
        var totalWeight=skillScores.Sum(s=>s.Weight);
        var score=new ReportScore(State(scored.Length,questions.Count,completed),
            scored.Length==0?null:general?R(scored.Average(q=>q.Assessment.Value!.Value)):totalWeight==0?null:R(skillScores.Sum(s=>s.Score*s.Weight)/totalWeight),
            scored.Length==0?null:general?R(scored.Average(q=>q.Assessment.Confidence!.Value)):totalWeight==0?null:R(skillScores.Sum(s=>s.Confidence*s.Weight)/totalWeight),
            general?"mean(scored_final_answers);unscored_excluded/v1":"weighted_mean(mean(scored_answers_per_skill),policy.skill_weights);unobserved_excluded/v1");
        if(score.Value is null && completed) score=score with {State="InsufficientEvidence"};
        var fitSkills=session.Skills.OrderBy(s=>s.SkillId).Select(s=> {
            var known=scored.Where(q=>snapshots[q.QuestionId].SkillId==s.SkillId && q.Difficulty>=s.TargetDifficulty).ToArray();
            var observed=questions.Any(q=>snapshots[q.QuestionId].SkillId==s.SkillId);
            var value=known.Length==0?(decimal?)null:R(known.Average(q=>q.Assessment.Value!.Value));
            return new ReportSkillFit(s.SkillId,s.Name,s.IsRequired,s.TargetDifficulty,weights.GetValueOrDefault(s.SkillId,1),
                value is null?observed?"TargetNotObserved":"NotObserved":value>=policy.PassingScore?"AtThreshold":"BelowThreshold",value,known.Length,known.Select(q=>q.QuestionId).ToArray());
        }).ToArray();
        var fitKnown=fitSkills.Where(s=>s.Value is not null && s.Weight>0).ToArray();
        var fitWeight=fitKnown.Sum(s=>s.Weight);var expectedWeight=fitSkills.Sum(s=>s.Weight);
        var fitCoverage=expectedWeight==0?0:fitWeight/expectedWeight;
        var fitValue=fitWeight==0?(decimal?)null:R(fitKnown.Sum(s=>s.Value!.Value*s.Weight)/fitWeight);
        var fitConfidence=fitWeight==0?(decimal?)null:R(fitKnown.Sum(s=>scored.Where(q=>snapshots[q.QuestionId].SkillId==s.SkillId && q.Difficulty>=s.TargetDifficulty).Average(q=>q.Assessment.Confidence!.Value)*s.Weight)/fitWeight);
        var classification=!completed?"InterviewIncomplete":fitValue is null || fitCoverage<policy.MinimumCoverage?"InsufficientCoverage":fitSkills.Any(s=>s.IsRequired && s.Value is null)?"RequiredSkillsUnobserved":fitValue>=policy.PassingScore?"AtConfiguredThreshold":"BelowConfiguredThreshold";
        var fit=new ReportPositionFit(!completed?"NotReady":fitValue is null?"InsufficientEvidence":fitKnown.Length==fitSkills.Count(s=>s.Weight>0)?"Available":"Partial",fitValue,fitConfidence,
            "weighted_mean(skill_quality_at_or_above_target_difficulty,policy.skill_weights);missing_excluded/v1",classification,R(fitCoverage),fitSkills,
            "تناسب فقط از شواهد مصاحبه در سطح هدف نقش محاسبه شده؛ احتمال موفقیت استخدام یا تصمیم نهایی نیست.");
        var distribution=new ReportDistribution(scored.Count(q=>q.Assessment.Value>=policy.StrongScore),scored.Count(q=>q.Assessment.Value>=policy.PassingScore && q.Assessment.Value<policy.StrongScore),
            scored.Count(q=>q.Assessment.Value<policy.PassingScore),questions.Count-scored.Length,policy.PassingScore,policy.StrongScore);
        ReportMetric Metric(AnswerDimensionDefinition definition) {
            var known=questions.Select(q=>new {Q=q,D=q.Assessment.Dimensions?.SingleOrDefault(d=>d.Code==definition.Code && d.State=="Succeeded")}).Where(x=>x.D?.Value is not null).ToArray();
            return new(definition.Code,definition.Title,State(known.Length,questions.Count,completed),known.Length==0?null:R(known.Average(x=>x.D!.Value!.Value)),
                known.Length==0?null:R(known.Average(x=>x.D!.Confidence!.Value)),known.Length,questions.Count,known.Select(x=>x.Q.QuestionId).ToArray(),known.SelectMany(x=>x.D!.Evidence).ToArray(),
                known.Length==0?"در پاسخ‌های موجود شاهد کافی برای این شاخص ثبت نشده است.":"میانگین ارزیابی‌های مستند همان شاخص؛ مقادیر نامعلوم در میانگین صفر محسوب نشده‌اند.");
        }
        var performance=new ReportMetric("performance","کیفیت پاسخ‌ها",score.State,score.Value,score.Confidence,scored.Length,questions.Count,scored.Select(q=>q.QuestionId).ToArray(),scored.SelectMany(q=>q.Assessment.Evidence).ToArray(),"بر اساس ارزیابی مستقل پاسخ‌های نهایی");
        ReportProfile Profile(bool isGeneral,bool applicable) {
            if(!applicable) return new("NotApplicable",isGeneral?"general":"specialized",[],[],distribution,"این نوع پروفایل برای نوع دیگر مصاحبه است.");
            var metrics=(isGeneral?AnswerDimensionProtocol.Catalog("general").Select(Metric):new[]{performance}.Concat(AnswerDimensionProtocol.Catalog("specialized").Select(Metric))).ToArray();
            var state=State(scored.Length,questions.Count,completed);
            if(state=="Available" && metrics.Any(m=>m.State!="Available")) state="Partial";
            return new(state,isGeneral?"general":"specialized",axes,metrics,distribution,"شاخص‌ها فقط به متن پاسخ و شواهد ثبت‌شده متکی‌اند؛ مقدار نامعلوم نشانه ضعف فرد نیست.");
        }
        ReportFinding Finding(ReportQuestion q)=>new(q.QuestionId,q.Answer!.AnswerId,q.Topic,q.Assessment.Value!.Value,
            $"سؤال {q.Number}، موضوع «{q.Topic}»: امتیاز {q.Assessment.Value:0.##} از ۱۰۰. {q.Assessment.Explanation}",q.Assessment.Evidence);
        var strong=scored.Where(q=>q.Assessment.Value>=policy.StrongScore).OrderByDescending(q=>q.Assessment.Value).ThenBy(q=>q.Number).Select(Finding).ToArray();
        var improve=scored.Where(q=>q.Assessment.Value<policy.PassingScore).OrderBy(q=>q.Assessment.Value).ThenBy(q=>q.Number).Select(Finding).ToArray();
        var gaps=questions.Where(q=>!Scored(q)).Select(q=>new ReportGap(q.Answer is null?"answer_missing":q.Assessment.FailureCode??q.Assessment.State,q.QuestionId,snapshots[q.QuestionId].SkillId,
            q.Answer is null?$"سؤال {q.Number} پاسخ نهایی ندارد؛ این مورد ضعف مهارت تلقی نشده است.":$"سؤال {q.Number} نمره قابل اتکا ندارد؛ وضعیت: {q.Assessment.State}."))
            .Concat(fitSkills.Where(s=>s.IsRequired && s.Value is null).Select(s=>new ReportGap("required_skill_unobserved",null,s.SkillId,$"برای مهارت الزامی «{s.Name}» در سطح هدف {s.TargetDifficulty} شواهد کافی نیست."))).ToList();
        if(policy.WarningCode is not null) gaps.Add(new(policy.WarningCode,null,null,"تنظیمات ذخیره‌شده معتبر نبود؛ تنظیمات پیش‌فرض آشکارا استفاده شده است."));
        var summary=$"{(completed?"مصاحبه پایان یافته است":"مصاحبه هنوز پایان نیافته است")}. از {questions.Count} سؤال پرسیده‌شده، {scored.Length} پاسخ نمره معتبر دارد؛ پوشش {coverage.Ratio*100:0.##} درصد است. "+
            (score.Value is null?"برای امتیاز کل شواهد کافی موجود نیست. ":$"امتیاز مشاهده‌شده {score.Value:0.##} از ۱۰۰ است{(coverage.IsProvisional?" و به دلیل پوشش ناقص، موقت محسوب می‌شود":"")}. ")+
            $"بر اساس آستانه‌های اعلام‌شده، {strong.Length} نقطه قوت و {improve.Length} زمینه بهبود از پاسخ‌های نمره‌دار استخراج شد. "+
            (classification is "InsufficientCoverage" or "RequiredSkillsUnobserved"?"پوشش شواهد برای نتیجه‌گیری درباره تناسب نقش کافی نیست. ":"تناسب نقش بر مبنای مهارت‌ها و سطح هدف، جداگانه نمایش داده شده است. ")+"این جمع‌بندی محاسباتی است؛ تصمیم نهایی با کارشناس است.";
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Version="comprehensive-report/1",session.Id,session.Status,session.PositionTitleSnapshot,session.PositionDescriptionSnapshot,
            Skills=session.Skills.OrderBy(s=>s.SkillId),Policy=policy with {CanEdit=false},Questions=questions
        }))));
        static ReportReviewSnapshot? Snapshot(string? json) {
            if(json is null) return null;
            try {var value=JsonSerializer.Deserialize<ReportReviewSnapshot>(json);return value?.Score is null || value.Coverage is null || value.Policy is null || value.Fit is null?null:value;}
            catch(JsonException){return null;}
        }
        var history=reviews.Where(e=>e.ActorUserId is not null).OrderBy(e=>e.RecordedAtUtc).ThenBy(e=>e.Id).Select(e=>new ReportReviewDecision(e.Id,e.ActorUserId!.Value,e.Kind,e.Note,e.RecordedAtUtc,e.ReportFingerprint,e.ReportFingerprint==fingerprint && Snapshot(e.ReportSnapshotJson) is not null,Snapshot(e.ReportSnapshotJson))).ToArray();
        var latest=history.LastOrDefault();
        var reviewState=!completed?"NotReady":latest is null?"AwaitingReview":latest.ReportFingerprint is null?"UnversionedReview":latest.MatchesCurrentReport?"Reviewed":"ReviewOutdated";
        var review=new ReportHumanReview(reviewState,canReview && completed,$"/api/interviews/{session.Id}/report/review",fingerprint,latest,history,
            reviewState switch {"AwaitingReview"=>"هنوز تصمیم انسانی ثبت نشده است؛ منتظر پردازش خودکار نیست.","ReviewOutdated"=>"گزارش پس از آخرین تصمیم تغییر کرده است؛ بازبینی مجدد لازم است.","UnversionedReview"=>"تصمیم قبلی ثبت شده اما به نسخه مشخصی از گزارش متصل نیست.","NotReady"=>"پس از پایان مصاحبه امکان ثبت تصمیم وجود دارد.",_=>"تصمیم انسانی به همین نسخه گزارش متصل است."});
        return new(score,new(completed?"Available":"Partial",summary),new(strong.Length>0?"Available":"NoFindings",strong.Select(x=>x.Text).ToArray(),strong),
            new(improve.Length>0?"Available":"NoFindings",improve.Select(x=>x.Text).ToArray(),improve),coverage,fit,Profile(false,!general),Profile(true,general),review,gaps,fingerprint);
    }
}
