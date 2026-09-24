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
