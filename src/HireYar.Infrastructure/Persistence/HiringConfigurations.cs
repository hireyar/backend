using HireYar.Domain.Hiring;
using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.FullName).HasMaxLength(200); b.Property(x => x.Email).HasMaxLength(256); b.Property(x => x.NormalizedEmail).HasMaxLength(256); b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        b.HasIndex(x => new { x.OrganizationId, x.NormalizedEmail }).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc, x.Id });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
    }
}
public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        b.HasIndex(x => new { x.OrganizationId, x.CandidateId, x.JobPositionId }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.JobPositionId, x.CreatedAtUtc, x.Id }); b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasOne<Candidate>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.CandidateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<JobPosition>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobPositionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_Application_Status", "\"Status\" IN ('Active','Withdrawn','Closed')"));
    }
}
public sealed class SessionConfiguration : IEntityTypeConfiguration<InterviewSession>
{
    public void Configure(EntityTypeBuilder<InterviewSession> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        foreach (var name in new[] { "OrganizationName", "JobTitle", "InterviewTitle", "CandidateName" }) b.Property<string>(name).HasMaxLength(200);
        b.Property(x => x.CandidateEmail).HasMaxLength(256); b.Property(x => x.Language).HasMaxLength(20); b.Property(x => x.ConsentVersion).HasMaxLength(50);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100); b.Property(x => x.RequestHash).HasMaxLength(64);
        b.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc, x.Id }); b.HasIndex(x => new { x.OrganizationId, x.Status, x.ExpiresAtUtc });
        b.HasOne<JobApplication>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobApplicationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<InterviewTemplate>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SourceInterviewTemplateId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Session_Status", "\"Status\" IN ('NotStarted','InProgress','Completed','Expired','Cancelled')");
            t.HasCheckConstraint("CK_Session_Window", "\"AvailableFromUtc\" < \"ExpiresAtUtc\"");
            t.HasCheckConstraint("CK_Session_Duration", "\"DurationMinutes\" BETWEEN 5 AND 60");
            t.HasCheckConstraint("CK_Session_FollowUps", "\"MaxFollowUpQuestionsPerQuestion\" BETWEEN 0 AND 3");
        });
    }
}
public sealed class SnapshotConfiguration : IEntityTypeConfiguration<SnapshotQuestion>
{
    public void Configure(EntityTypeBuilder<SnapshotQuestion> b)
    {
        b.ToTable("SnapshotQuestions"); b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.InterviewSessionId, x.Id });
        b.Property(x => x.Text).HasMaxLength(2000); b.Property(x => x.Competency).HasMaxLength(200); b.Property(x => x.EvaluationGuidance).HasMaxLength(4000);
        b.HasOne<InterviewSession>().WithMany(x => x.Questions).HasForeignKey(x => x.InterviewSessionId);
        b.HasIndex(x => new { x.InterviewSessionId, x.Order }).IsUnique(); b.ToTable(t => t.HasCheckConstraint("CK_Snapshot_Order", "\"Order\" BETWEEN 1 AND 30"));
    }
}
public sealed class InterviewInvitationConfiguration : IEntityTypeConfiguration<InterviewInvitation>
{
    public void Configure(EntityTypeBuilder<InterviewInvitation> b)
    {
        b.HasKey(x => x.InterviewSessionId); b.Property(x => x.TokenHash).HasMaxLength(64); b.Property(x => x.CredentialHash).HasMaxLength(64);
        b.Property(x => x.DeliveryStatus).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => x.CredentialHash).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.DeliveryStatus });
        b.HasOne<InterviewSession>().WithOne(x => x.Invitation).HasForeignKey<InterviewInvitation>(x => new { x.OrganizationId, x.InterviewSessionId }).HasPrincipalKey<InterviewSession>(x => new { x.OrganizationId, x.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_InterviewInvitation_Delivery", "\"DeliveryStatus\" IN ('Pending','Sent','Failed')"));
    }
}
public sealed class AnswerConfiguration : IEntityTypeConfiguration<InterviewAnswer>
{
    public void Configure(EntityTypeBuilder<InterviewAnswer> b)
    {
        b.ToTable("InterviewAnswers"); b.HasKey(x => x.Id); b.Property(x => x.Text).HasMaxLength(10000); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever(); b.HasIndex(x => new { x.InterviewSessionId, x.SnapshotQuestionId }).IsUnique();
        b.HasOne<InterviewSession>().WithMany(x => x.Answers).HasForeignKey(x => x.InterviewSessionId);
        b.HasOne<SnapshotQuestion>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.SnapshotQuestionId }).HasPrincipalKey(x => new { x.InterviewSessionId, x.Id });
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Answer_Status", "\"Status\" IN ('Draft','Submitted','Skipped')");
            t.HasCheckConstraint("CK_Answer_Content", "(\"Status\" <> 'Skipped' OR \"Text\" IS NULL) AND (\"Status\" <> 'Submitted' OR length(btrim(\"Text\")) > 0 AND \"Text\" IS NOT NULL)");
        });
    }
}
