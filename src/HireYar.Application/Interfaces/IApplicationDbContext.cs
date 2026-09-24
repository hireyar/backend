using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Common;

public interface IApplicationDbContext
{
    DbSet<HireYar.Domain.Hiring.Candidate> Candidates { get; }
    DbSet<HireYar.Domain.Hiring.JobApplication> JobApplications { get; }
    DbSet<HireYar.Domain.Hiring.InterviewSession> InterviewSessions { get; }
    DbSet<HireYar.Domain.Hiring.InterviewInvitation> InterviewInvitations { get; }
    DbSet<HireYar.Domain.Hiring.InterviewAnswer> InterviewAnswers { get; }
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginHiringTransactionAsync(Guid organizationId, CancellationToken ct);
    Task LockInterviewSourcesAsync(Guid organizationId, Guid jobId, Guid templateId, CancellationToken ct);
    DbSet<Organization> Organizations { get; }
    DbSet<JobPosition> JobPositions { get; }
    DbSet<InterviewTemplate> InterviewTemplates { get; }
    DbSet<InterviewQuestion> InterviewQuestions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    void SetOriginalVersion(object entity, byte[] version);
    Task ReplaceQuestionsAsync(InterviewTemplate template, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct);
}
