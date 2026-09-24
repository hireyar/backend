using HireYar.Application.Common;
using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Members;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<HireYar.Domain.Hiring.Candidate> Candidates => Set<HireYar.Domain.Hiring.Candidate>();
    public DbSet<HireYar.Domain.Hiring.JobApplication> JobApplications => Set<HireYar.Domain.Hiring.JobApplication>();
    public DbSet<HireYar.Domain.Hiring.InterviewSession> InterviewSessions => Set<HireYar.Domain.Hiring.InterviewSession>();
    public DbSet<HireYar.Domain.Hiring.InterviewInvitation> InterviewInvitations => Set<HireYar.Domain.Hiring.InterviewInvitation>();
    public DbSet<HireYar.Domain.Hiring.InterviewAnswer> InterviewAnswers => Set<HireYar.Domain.Hiring.InterviewAnswer>();
    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginHiringTransactionAsync(Guid organizationId, CancellationToken ct)
    {
        var tx = await Database.BeginTransactionAsync(ct);
        try
        {
            await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Organizations\" WHERE \"Id\" = {organizationId} FOR UPDATE", ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    public async Task LockInterviewSourcesAsync(Guid organizationId, Guid jobId, Guid templateId, CancellationToken ct)
    {
        await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"JobPositions\" WHERE \"Id\" = {jobId} AND \"OrganizationId\" = {organizationId} FOR UPDATE", ct);
        await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"InterviewTemplates\" WHERE \"Id\" = {templateId} AND \"OrganizationId\" = {organizationId} FOR UPDATE", ct);
    }
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<InterviewTemplate> InterviewTemplates => Set<InterviewTemplate>();
    public DbSet<InterviewQuestion> InterviewQuestions => Set<InterviewQuestion>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        foreach (var relation in b.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys())) relation.DeleteBehavior = DeleteBehavior.Restrict;
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ChangeTracker.DetectChanges();
        // PostgreSQL has no SQL Server rowversion. Keep the opaque Base64 API token
        // and rotate a 128-bit application-managed concurrency token on every write.
        foreach (var entry in ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Added or EntityState.Modified &&
            e.Metadata.FindProperty("RowVersion") is not null))
            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        return base.SaveChangesAsync(ct);
    }
    public void SetOriginalVersion(object entity, byte[] version) => Entry(entity).Property("RowVersion").OriginalValue = version;
    public async Task ReplaceQuestionsAsync(InterviewTemplate template, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct)
    {
        await using var tx = await Database.BeginTransactionAsync(ct);
        // Persist the parent version check before replacing children. Delete/insert permits order swaps under a unique index.
        await SaveChangesAsync(ct);
        InterviewQuestions.RemoveRange(template.Questions);
        await SaveChangesAsync(ct);
        template.Questions = questions.ToList();
        InterviewQuestions.AddRange(template.Questions);
        await SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
