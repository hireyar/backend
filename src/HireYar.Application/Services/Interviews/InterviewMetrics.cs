using System.Diagnostics.Metrics;
namespace HireYar.Application.Interviews;
public static class InterviewMetrics
{
    private static readonly Meter Meter = new("HireYar.Interviews", "1.0");
    public static readonly Counter<long> Started = Meter.CreateCounter<long>("hireyar.interview.started");
    public static readonly Counter<long> Completed = Meter.CreateCounter<long>("hireyar.interview.completed");
    public static readonly Counter<long> Selection = Meter.CreateCounter<long>("hireyar.interview.question.selection");
    public static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>("hireyar.interview.answer.processing.duration", "ms");
    public static readonly Histogram<double> SttDuration = Meter.CreateHistogram<double>("hireyar.interview.stt.duration", "ms");
    public static readonly Histogram<double> LlmDuration = Meter.CreateHistogram<double>("hireyar.interview.llm.duration", "ms");
    public static readonly Histogram<double> TtsDuration = Meter.CreateHistogram<double>("hireyar.interview.tts.duration", "ms");
    public static readonly Counter<long> SttFailure = Meter.CreateCounter<long>("hireyar.interview.stt.failure");
    public static readonly Counter<long> LlmFailure = Meter.CreateCounter<long>("hireyar.interview.llm.failure");
    public static readonly Counter<long> TtsFailure = Meter.CreateCounter<long>("hireyar.interview.tts.failure");
}
