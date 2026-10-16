using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Application.Billing;
using HireYar.Domain.Interviews;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace HireYar.Application.Interviews;

// Reading persisted facts and assessments never invokes AI or mutates report state.
public sealed class SessionReportService(IApplicationDbContext db, ICurrentUser user, InterviewBillingService billing, TimeProvider time, AnswerAssessmentOptions assessmentOptions)
{
    public async Task<SessionReportDocument> ReadAsync(Guid id, CancellationToken ct)
    {
        var session = await db.InterviewSessions.IgnoreAutoIncludes().AsNoTracking().Include(s => s.Questions).Include(s => s.Answers).Include(s => s.Skills)
            .SingleOrDefaultAsync(s => s.Id == id && s.OrganizationId == user.OrganizationId && s.DeletedAtUtc == null, ct)
            ?? throw AppException.NotFound();
        var candidate = await db.CandidateApplications.AsNoTracking().SingleAsync(a => a.Id == session.CandidateApplicationId && a.OrganizationId == user.OrganizationId, ct);
        var contract = await db.InterviewBillings.AsNoTracking().SingleOrDefaultAsync(b => b.OrganizationId == user.OrganizationId && b.InterviewSessionId == id, ct);
        var included = contract is null || (await billing.CapabilitiesAsync(session, ct)).Contains("interview_report");
        if (!included) throw new AppException(403, "feature_unavailable", "The interview contract does not include reports.");
        var mediaAllowed = true;
        try { await billing.RequireMediaRetentionAsync(session, ct); }
        catch (AppException e) when (e.Status == 404) { mediaAllowed = false; }
        ReportTurn Turn(InterviewAnswer a) => new(a.Id, a.Status.ToString(), a.Transcript, a.AssistantMessage,
            a.SubmittedAtUtc, a.DurationSeconds, mediaAllowed ? $"/api/interviews/{id}/answers/{a.Id}/audio" : null,
            mediaAllowed && a.VideoStorageKey is not null ? $"/api/interviews/{id}/answers/{a.Id}/video" : null);
        var type = session.RequiredCapabilityCode == "hr_interview" ? "general" : "specialized";
        var assessments = await db.AnswerAssessments.AsNoTracking().Where(a => a.OrganizationId == user.OrganizationId && a.InterviewSessionId == id).ToDictionaryAsync(a => a.AnswerId, ct);
        var questions = session.Questions.Where(q => q.AskedAtUtc is not null || session.Answers.Any(a => a.QuestionId == q.QuestionId))
            .OrderBy(q => q.AskedAtUtc).ThenBy(q => q.Order).Select((q, index) => {
                var turns = session.Answers.Where(a => a.QuestionId == q.QuestionId).OrderBy(a => a.SubmittedAtUtc).ThenBy(a => a.Id).ToArray();
                var answer = turns.SingleOrDefault(a => !a.IsInteraction);
                return new ReportQuestion(q.QuestionId, index + 1, q.Text, q.Topic, q.Difficulty, q.AskedAtUtc,
                    answer?.Status.ToString() ?? "Unanswered", answer is null ? null : Turn(answer),
                    turns.Where(a => a.IsInteraction).Select(Turn).ToArray(), AnswerAssessmentService.View(answer, session.Status == InterviewStatus.Completed,
                        answer is not null ? assessments.GetValueOrDefault(answer.Id) : null, time.GetUtcNow(), type));
            }).ToArray();
        var answered = questions.Count(q => q.Answer is { State: "Processed", Transcript: not null });
        var durations = questions.Where(q => q.Answer?.DurationSeconds is not null).Select(q => (decimal)q.Answer!.DurationSeconds!.Value).ToArray();
        var events = await db.InterviewReviewEntries.AsNoTracking().Where(e => e.OrganizationId == user.OrganizationId && e.InterviewSessionId == id && e.ActorUserId == null)
            .OrderBy(e => e.ClientOccurredAtUtc).ThenBy(e => e.RecordedAtUtc).ThenBy(e => e.Id).ToArrayAsync(ct);
        var consent = events.LastOrDefault(e => e.Kind is "consent_accepted" or "consent_revoked")?.Kind;
        var recording = await db.InterviewSessionRecordings.AsNoTracking().SingleOrDefaultAsync(r => r.OrganizationId == user.OrganizationId && r.InterviewSessionId == id, ct);
        var segments = await db.InterviewRecordingSegments.AsNoTracking().Where(r => r.OrganizationId == user.OrganizationId && r.InterviewSessionId == id)
            .OrderBy(r => r.SegmentNumber).ToArrayAsync(ct);
        ReportRecording Recording(string json, int duration, int? number) {
            var videoUrl = !mediaAllowed ? null : number is null ? $"/api/interviews/{id}/recording/video" : $"/api/interviews/{id}/recording/segments/{number}/video";
            var validIds = session.Answers.Select(a => a.Id).ToHashSet();
            ReportOffset[] timeline;
            try {
                timeline = JsonSerializer.Deserialize<ReportOffset[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
                if (timeline.Any(t => t is null || !validIds.Contains(t.AnswerId) || t.StartSeconds < 0 || t.EndSeconds <= t.StartSeconds || t.EndSeconds > duration))
                    return new(videoUrl, duration, number, "Unavailable", []);
            }
            catch (JsonException) { return new(videoUrl, duration, number, "Unavailable", []); }
            return new(videoUrl, duration, number, "CandidateReported", mediaAllowed ? timeline : []);
        }
        var policyRow=await db.SessionReportPolicies.AsNoTracking().SingleOrDefaultAsync(p=>p.OrganizationId==user.OrganizationId && p.InterviewSessionId==id,ct);
        var policy=SessionReportPolicyService.Read(session.Skills,policyRow,user.Role==OrganizationRole.Owner);
        var decisions=await db.InterviewReviewEntries.AsNoTracking().Where(e=>e.OrganizationId==user.OrganizationId && e.InterviewSessionId==id && e.ActorUserId!=null)
            .OrderBy(e=>e.RecordedAtUtc).ThenBy(e=>e.Id).ToArrayAsync(ct);
        var analytics=ComprehensiveReportBuilder.Build(session,questions,policy,decisions,user.Role==OrganizationRole.Owner);
        var shouldPoll=assessmentOptions.WorkerEnabled && questions.Any(q=>q.Assessment.IsPending);
        var sections=new[] {
            new ReportSection("navigation","دسترسی‌ها و خروجی گزارش","Available","چاپ و رسانه خصوصی جلسه"),
            new ReportSection("identity","شناسنامه کارجو و مصاحبه","Available","اطلاعات ثبت‌شده جلسه"),
            new ReportSection("score","امتیاز کل",analytics.Score.State,"امتیاز مشاهده‌شده با فرمول و پوشش شواهد مشخص",shouldPoll),
            new ReportSection("statistics","آمار مصاحبه","Available","آمار سؤال‌های واقعی و توزیع نمرات"),
            new ReportSection("summary","جمع‌بندی مصاحبه",analytics.Summary.State,"جمع‌بندی محاسباتی از شواهد و پوشش موجود",shouldPoll),
            new ReportSection("strengthsAndImprovements","نقاط قوت و زمینه‌های بهبود",analytics.Coverage.ScoredQuestions>0?"Available":"InsufficientEvidence","با آستانه‌های مشخص و ارجاع به پاسخ؛ کمبود داده ضعف فرد نیست",shouldPoll),
            new ReportSection("specializedProfile","پروفایل تخصصی",analytics.Specialized.State,analytics.Specialized.Message,type=="specialized" && shouldPoll),
            new ReportSection("generalProfile","شاخص‌های مصاحبه عمومی",analytics.General.State,analytics.General.Message,type=="general" && shouldPoll),
            new ReportSection("questions","جزئیات سؤال‌ها و پاسخ‌ها","Available","سؤال، پاسخ، تعامل و ارزیابی مستقل",shouldPoll),
            new ReportSection("signals","رسانه و رویدادهای جلسه","Available","نبود رویداد به معنی سلامت یا ضبط کامل نیست"),
            new ReportSection("humanReview","بررسی و تصمیم کارشناس",analytics.Review.State,analytics.Review.Message)
        };
        var title = session.PositionTitleSnapshot ?? await db.JobPositions.Where(p => p.OrganizationId == user.OrganizationId && p.Id == session.JobPositionId).Select(p => p.Title).SingleAsync(ct);
        var elapsed = session.StartedAtUtc is { } start && session.CompletedAtUtc is { } end && end >= start ? (int?)(end - start).TotalSeconds : null;
        return new("session-report/1", id, session.Status == InterviewStatus.Completed ? "BaseReady" : "InterviewIncomplete",
            new(candidate.Id, (candidate.FirstName + " " + candidate.LastName).Trim(), title, type, session.Status.ToString(), session.CreatedAtUtc,
                session.StartedAtUtc, session.CompletedAtUtc, elapsed, session.CompletionReason?.ToString()),
            new(questions.Length, session.Questions.Count, questions.Count(q => q.Answer is not null), answered, questions.Count(q => q.Answer is null),
                questions.Count(q => q.AnswerState == "Failed"), questions.Sum(q => q.Clarifications.Count),
                durations.Length == 0 ? null : Math.Round(durations.Average(), 2), questions.Select(q => q.Difficulty).Distinct().Order().ToArray()),
            new(included, mediaAllowed ? "WithinRetention" : "Expired", $"/api/interviews/{id}/report/print"), questions,
            new(!mediaAllowed ? "Expired" : recording is not null || segments.Length > 0 ? "UploadedUnverified" : "Missing",
                recording is null ? null : Recording(recording.TimelineJson, recording.DurationSeconds, null),
                segments.Select(r => Recording(r.TimelineJson, r.DurationSeconds, r.SegmentNumber)).ToArray()),
            new(consent == "consent_accepted" ? "Accepted" : consent == "consent_revoked" ? "Revoked" : "Unknown",
                events.Any(e => e.Kind is "microphone_disconnected" or "camera_disconnected") ? "ReportedIssue" : "Unknown",
                events.Select(e => new ReportSignal(e.Kind, e.ClientOccurredAtUtc, e.RecordedAtUtc)).ToArray()),
            analytics.Score, analytics.Summary, analytics.Strengths, analytics.Improvements, sections,
            ReportProcessingStatus.From(questions.Select(q => q.Assessment).ToArray(), sections, assessmentOptions),
            analytics.Coverage,analytics.Fit,analytics.Specialized,analytics.General,policy,analytics.Review,analytics.Gaps,analytics.Fingerprint);
    }
}
