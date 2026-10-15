using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed class FakeAnswerAssessmentProvider : IAnswerAssessmentProvider
{
    public int Calls;
    public Func<AnswerAssessmentInput, CancellationToken, Task<AnswerAssessmentResponse>>? Handler;
    public Task<AnswerAssessmentResponse> AssessAnswerAsync(AnswerAssessmentInput input, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Handler?.Invoke(input, ct) ?? Task.FromResult(Valid());
    }
    public static AnswerAssessmentResponse Valid(int score = 80) => new(
        "{\"verdict\":\"scored\",\"score\":" + score + ",\"confidence\":0.8,\"explanation\":\"ارزیابی نمونه مبتنی بر پاسخ ثبت‌شده\",\"evidenceIds\":[1]}", "Synthetic", "synthetic-assessor");
    public void Reset() { Handler = null; Calls = 0; }
}
