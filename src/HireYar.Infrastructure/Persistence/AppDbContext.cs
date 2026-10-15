using HireYar.Domain.Billing;
using HireYar.Application.Common;
using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Members;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HireYar.Domain.Interviews;
namespace HireYar.Infrastructure.Persistence;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<RegistrationTrial> RegistrationTrials => Set<RegistrationTrial>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<InterviewTemplate> InterviewTemplates => Set<InterviewTemplate>();
    public DbSet<InterviewQuestion> InterviewQuestions => Set<InterviewQuestion>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<MobileTeamInvitation> MobileTeamInvitations => Set<MobileTeamInvitation>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<CandidateApplication> CandidateApplications => Set<CandidateApplication>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<PositionSkill> PositionSkills => Set<PositionSkill>();
    public DbSet<BankQuestion> BankQuestions => Set<BankQuestion>();
    public DbSet<QuestionAudio> QuestionAudios => Set<QuestionAudio>();
    public DbSet<InterviewSession> InterviewSessions => Set<InterviewSession>();
    public DbSet<InterviewAnswer> InterviewAnswers => Set<InterviewAnswer>();
    public DbSet<AnswerReportAssessment> AnswerAssessments => Set<AnswerReportAssessment>();
    public Task ReloadAnswerAssessmentAsync(AnswerReportAssessment assessment, CancellationToken ct) => Entry(assessment).ReloadAsync(ct);
    public DbSet<InterviewIntroduction> InterviewIntroductions => Set<InterviewIntroduction>();
    public DbSet<InterviewReviewEntry> InterviewReviewEntries => Set<InterviewReviewEntry>();
    public DbSet<InterviewSessionRecording> InterviewSessionRecordings => Set<InterviewSessionRecording>();
    public DbSet<InterviewRecordingSegment> InterviewRecordingSegments => Set<InterviewRecordingSegment>();
    public Task ReloadIntroductionAsync(InterviewIntroduction introduction, CancellationToken ct) => Entry(introduction).ReloadAsync(ct);
    public Task ReloadInterviewAsync(InterviewSession session, CancellationToken ct) => Entry(session).ReloadAsync(ct);
    public void DetachEntity(object entity) => Entry(entity).State = EntityState.Detached;
    public DbSet<PricingPackage> PricingPackages => Set<PricingPackage>();
    public IQueryable<PricingPackage> PricingCatalog => PricingPackages.AsSingleQuery();
    public DbSet<PackagePeriod> PackagePeriods => Set<PackagePeriod>();
    public DbSet<FeatureDefinition> FeatureDefinitions => Set<FeatureDefinition>();
    public DbSet<PackageFeature> PackageFeatures => Set<PackageFeature>();
    public DbSet<AiCreditPackage> AiCreditPackages => Set<AiCreditPackage>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<TeamSeatPurchase> TeamSeatPurchases => Set<TeamSeatPurchase>();
    public DbSet<SubscriptionPurchase> SubscriptionPurchases => Set<SubscriptionPurchase>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();
    public DbSet<SubscriptionReplacement> SubscriptionReplacements => Set<SubscriptionReplacement>();
    public DbSet<OrganizationAiWallet> OrganizationAiWallets => Set<OrganizationAiWallet>();
    public DbSet<AiCreditTransaction> AiCreditTransactions => Set<AiCreditTransaction>();
    public DbSet<AiCreditReservation> AiCreditReservations => Set<AiCreditReservation>();
    public DbSet<InterviewBilling> InterviewBillings => Set<InterviewBilling>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        BillingConfigurations.Configure(b);
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        foreach (var relation in b.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys())) relation.DeleteBehavior = DeleteBehavior.Restrict;
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ChangeTracker.DetectChanges();
        ValidateBillingHistory();
        // PostgreSQL has no SQL Server rowversion. Keep the opaque Base64 API token
        // and rotate a 128-bit application-managed concurrency token on every write.
        foreach (var entry in ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Added or EntityState.Modified &&
            (e.Entity is JobPosition || e.Entity is InterviewTemplate || e.Entity is InterviewSession || e.Entity is InterviewIntroduction || e.Entity is BankQuestion || e.Entity is PricingPackage || e.Entity is AiCreditPackage)))
            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        return base.SaveChangesAsync(ct);
    }
    public void SetOriginalVersion(object entity, byte[] version) => Entry(entity).Property("RowVersion").OriginalValue = version;
    public async Task ReplaceQuestionsAsync(InterviewTemplate template, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct)
    {
        // Join the organization transaction when called by template mutations.
        await using var tx = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(ct) : null;
        // Persist the parent version check before replacing children. Delete/insert permits order swaps under a unique index.
        await SaveChangesAsync(ct);
        InterviewQuestions.RemoveRange(template.Questions);
        await SaveChangesAsync(ct);
        template.Questions = questions.ToList();
        InterviewQuestions.AddRange(template.Questions);
        await SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }
}
