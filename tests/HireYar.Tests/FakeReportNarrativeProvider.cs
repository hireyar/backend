using System.Text.Json;
using HireYar.Application.Interviews;
namespace HireYar.Tests;
public sealed class FakeReportNarrativeProvider : IReportNarrativeProvider
{
    public int Calls;
    public Func<ReportNarrativeInput,CancellationToken,Task<AnswerAssessmentResponse>>? Handler;
    public Task<AnswerAssessmentResponse> GenerateNarrativeAsync(ReportNarrativeInput input,CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Handler?.Invoke(input,ct) ?? Task.FromResult(Valid(input));
    }
    public static AnswerAssessmentResponse Valid(ReportNarrativeInput input) {
        var id=input.Sources.First(s=>s.Kind=="answer").Id;
        return new(JsonSerializer.Serialize(new {
            summary=new{text="روایت آزمایشی با شاهد ثبت‌شده <script>untrusted</script>",evidenceIds=new[]{id}},
            nextStep=new{text="بررسی مثال عملی در مصاحبه تکمیلی",evidenceIds=new[]{id}},
            followUpQuestions=new[]{new{text="مثال عملی مرتبط با پاسخ خود را توضیح دهید.",topic="مثال شغلی",reason="نیاز به بررسی جزئیات مثال",evidenceIds=new[]{id}}},
            experienceSourceIds=input.Sources.Where(s=>s.Kind=="resume").Take(1).Select(s=>s.Id).ToArray()
        }),"Synthetic","synthetic-narrative");
    }
    public void Reset() {Calls=0;Handler=null;}
}