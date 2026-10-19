using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
using HireYar.Domain.Interviews;
namespace HireYar.Application.Common;

public interface IApplicationDbContext
{
    // Provider-specific locking stays in Infrastructure. Callback must contain no network I/O.
    Task<T> WithOrganizationLockAsync<T>(Guid organizationId, Func<Task<T>> action, CancellationToken ct);
    DbSet<HireYar.Domain.Support.SupportTicket> SupportTickets { get; }
    DbSet<HireYar.Domain.Support.TicketMessage> TicketMessages { get; }
    DbSet<HireYar.Domain.Support.TicketAttachment> TicketAttachments { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<JobPosition> JobPositions { get; }
    DbSet<InterviewTemplate> InterviewTemplates { get; }
    DbSet<InterviewQuestion> InterviewQuestions { get; }
    DbSet<CandidateApplication> CandidateApplications { get; }
    DbSet<Skill> Skills { get; }
    DbSet<PositionSkill> PositionSkills { get; }
    DbSet<BankQuestion> BankQuestions { get; }
    DbSet<QuestionAudio> QuestionAudios { get; }
    DbSet<InterviewSession> InterviewSessions { get; }
    DbSet<InterviewAnswer> InterviewAnswers { get; }
    DbSet<AnswerReportAssessment> AnswerAssessments { get; }
    DbSet<SessionReportNarrative> SessionReportNarratives { get; }
    DbSet<SessionReportPolicy> SessionReportPolicies { get; }
    Task ReloadAnswerAssessmentAsync(AnswerReportAssessment assessment, CancellationToken ct);
    DbSet<InterviewIntroduction> InterviewIntroductions { get; }
    DbSet<InterviewReviewEntry> InterviewReviewEntries { get; }
    DbSet<InterviewSessionRecording> InterviewSessionRecordings { get; }
    DbSet<InterviewRecordingSegment> InterviewRecordingSegments { get; }
    Task ReloadIntroductionAsync(InterviewIntroduction introduction, CancellationToken ct);
    Task ReloadInterviewAsync(InterviewSession session, CancellationToken ct);
    void DetachEntity(object entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void SetOriginalVersion(object entity, byte[] version);
    Task ReplaceQuestionsAsync(InterviewTemplate template, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct);
}
