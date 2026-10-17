using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public static class ReportExpectedLevelBuilder
{
    public static ReportExpectedLevel Build(InterviewSession session, IReadOnlyList<ReportQuestion> questions)
    {
        var snapshots=session.Questions.ToDictionary(q=>q.QuestionId);
        var skills=session.Skills.ToDictionary(s=>s.SkillId);
        var answers=questions.Where(q=>q.Answer is not null && q.Assessment.State=="Succeeded" && q.Assessment.Value is not null && q.Assessment.Evidence.Count>0)
            .Select(q=> {
                var target=skills[snapshots[q.QuestionId].SkillId].TargetDifficulty;
                return new ReportExpectedLevelAnswer(q.QuestionId,q.Topic,q.Difficulty,target,q.Difficulty<target?"BelowTarget":q.Difficulty==target?"AtTarget":"AboveTarget");
            }).ToArray();
        return new(session.Status!=InterviewStatus.Completed?"NotReady":answers.Length==0?"InsufficientEvidence":answers.Length==questions.Count?"Available":"Partial",
            answers.Count(a=>a.Relation=="BelowTarget"),answers.Count(a=>a.Relation=="AtTarget"),answers.Count(a=>a.Relation=="AboveTarget"),answers,
            "مقایسه دشواری سؤال‌های دارای پاسخ ارزیابی‌شده با سطح هدف snapshot نقش؛ این شمارش، نمره کیفیت یا احتمال موفقیت استخدام نیست.");
    }
}