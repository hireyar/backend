using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
using HireYar.Domain.Interviews;
namespace HireYar.Application.Common;

public interface IApplicationDbContext
{
    DbSet<HireYar.Domain.Billing.RegistrationTrial> RegistrationTrials { get; }
    DbSet<HireYar.Domain.Billing.PricingPackage> PricingPackages { get; }
    // Infrastructure supplies one SQL statement for contract reads, overriding its split-query default.
    IQueryable<HireYar.Domain.Billing.PricingPackage> PricingCatalog { get; }
    DbSet<HireYar.Domain.Billing.PackagePeriod> PackagePeriods { get; }
    DbSet<HireYar.Domain.Billing.FeatureDefinition> FeatureDefinitions { get; }
    DbSet<HireYar.Domain.Billing.PackageFeature> PackageFeatures { get; }
    DbSet<HireYar.Domain.Billing.AiCreditPackage> AiCreditPackages { get; }
    DbSet<HireYar.Domain.Billing.Payment> Payments { get; }
    DbSet<HireYar.Domain.Billing.TeamSeatPurchase> TeamSeatPurchases { get; }
    DbSet<HireYar.Domain.Billing.SubscriptionPurchase> SubscriptionPurchases { get; }
    DbSet<HireYar.Domain.Billing.OrganizationSubscription> OrganizationSubscriptions { get; }
    DbSet<HireYar.Domain.Billing.SubscriptionReplacement> SubscriptionReplacements { get; }
    DbSet<HireYar.Domain.Billing.OrganizationAiWallet> OrganizationAiWallets { get; }
    DbSet<HireYar.Domain.Billing.AiCreditTransaction> AiCreditTransactions { get; }
    DbSet<HireYar.Domain.Billing.AiCreditReservation> AiCreditReservations { get; }
    DbSet<HireYar.Domain.Billing.InterviewBilling> InterviewBillings { get; }
    // Provider-specific locking stays in Infrastructure. Callback must contain no network I/O.
    Task<T> WithOrganizationBillingLockAsync<T>(Guid organizationId, Func<Task<T>> action, CancellationToken ct);
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
