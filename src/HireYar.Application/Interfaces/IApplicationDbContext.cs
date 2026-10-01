using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
using HireYar.Domain.Interviews;
namespace HireYar.Application.Common;

public interface IApplicationDbContext
{
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
    Task ReloadInterviewAsync(InterviewSession session, CancellationToken ct);
    void DetachEntity(object entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void SetOriginalVersion(object entity, byte[] version);
    Task ReplaceQuestionsAsync(InterviewTemplate template, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct);
}
