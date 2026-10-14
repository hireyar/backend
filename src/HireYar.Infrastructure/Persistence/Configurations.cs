using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using HireYar.Infrastructure.Auth;
using HireYar.Infrastructure.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b) { b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); }
}
public sealed class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256); b.Property(x => x.NormalizedEmail).HasMaxLength(256);
        b.Property(x => x.PhoneNumber).HasMaxLength(11);
        b.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL");
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.NormalizedEmail).IsUnique(); b.HasIndex(x => new { x.OrganizationId, x.IsActive });
        b.HasIndex(x => x.OrganizationId).IsUnique().HasFilter("\"IsActive\" = TRUE AND \"Role\" = 'Owner'");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.ToTable(t => { t.HasCheckConstraint("CK_User_Role", "\"Role\" IN ('Owner','Recruiter')"); });
    }
}
public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Mobile).HasMaxLength(11).IsRequired();
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Mobile).IsUnique();
    }
}
public sealed class JobConfiguration : IEntityTypeConfiguration<JobPosition>
{
    public void Configure(EntityTypeBuilder<JobPosition> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Title).HasMaxLength(200).IsRequired(); b.Property(x => x.Description).HasMaxLength(10000).IsRequired();
        b.Property(x => x.Seniority).HasMaxLength(100).IsRequired(); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever(); b.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAtUtc, x.Id });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.ToTable(t => t.HasCheckConstraint("CK_Job_Status", "\"Status\" IN ('Draft','Active','Archived')"));
    }
}
public sealed class TemplateConfiguration : IEntityTypeConfiguration<InterviewTemplate>
{
    public void Configure(EntityTypeBuilder<InterviewTemplate> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200).IsRequired(); b.Property(x => x.Language).HasMaxLength(20).IsRequired();
        b.Property(x => x.InterviewType).HasMaxLength(20).IsRequired();
        b.Property(x => x.WelcomeMessage).HasMaxLength(2000).IsRequired();
        b.Property(x => x.GoodbyeMessage).HasMaxLength(2000).IsRequired();
        b.Property(x => x.CandidateIntroductionMessage).HasMaxLength(2000).IsRequired();
        b.Property(x => x.CompanyIntroductionMessage).HasMaxLength(2000).IsRequired();
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Template_CandidateIntroduction", "NOT \"CandidateIntroductionEnabled\" OR length(btrim(\"CandidateIntroductionMessage\")) > 0");
            t.HasCheckConstraint("CK_Template_CompanyIntroduction", "NOT \"CompanyIntroductionEnabled\" OR length(btrim(\"CompanyIntroductionMessage\")) > 0");
        });
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever(); b.HasIndex(x => new { x.OrganizationId, x.JobPositionId, x.CreatedAtUtc, x.Id });
        b.HasOne<JobPosition>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobPositionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Template_Duration", "\"DurationMinutes\" BETWEEN 5 AND 60");
            t.HasCheckConstraint("CK_Template_Type", "\"InterviewType\" IN ('general','specialized')");
            t.HasCheckConstraint("CK_Template_FollowUps", "\"MaxFollowUpQuestionsPerQuestion\" BETWEEN 0 AND 3");
        });
    }
}
public sealed class QuestionConfiguration : IEntityTypeConfiguration<InterviewQuestion>
{
    public void Configure(EntityTypeBuilder<InterviewQuestion> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Text).HasMaxLength(2000).IsRequired(); b.Property(x => x.Competency).HasMaxLength(200).IsRequired();
        b.Property(x => x.EvaluationGuidance).HasMaxLength(4000).IsRequired(); b.HasIndex(x => new { x.InterviewTemplateId, x.Order }).IsUnique();
        b.HasOne<InterviewTemplate>().WithMany(x => x.Questions).HasForeignKey(x => x.InterviewTemplateId);
        b.Property(x => x.TopicName).HasMaxLength(200);
        b.Property(x => x.Difficulty).HasDefaultValue(3);
        b.HasIndex(x => new { x.InterviewTemplateId, x.TopicOrder, x.Difficulty }).IsUnique().HasFilter("\"TopicOrder\" IS NOT NULL");
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Question_Order", "\"Order\" BETWEEN 1 AND 30");
            t.HasCheckConstraint("CK_Question_Topic", "(\"TopicOrder\" IS NULL AND \"TopicName\" IS NULL AND \"Difficulty\" = 3) OR (\"TopicOrder\" IS NOT NULL AND \"TopicOrder\" BETWEEN 1 AND 10 AND \"TopicName\" IS NOT NULL AND length(trim(\"TopicName\")) > 0 AND \"Difficulty\" IN (1,3,5))");
        });
    }
}
public sealed class RefreshConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Hash).HasMaxLength(64).IsUnicode(false).IsRequired(); b.HasIndex(x => x.Hash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.FamilyId }); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId);
        b.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedById);
    }
}
public sealed class InvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Email).HasMaxLength(256).IsRequired(); b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired(); b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.NormalizedEmail }).IsUnique().HasFilter("\"AcceptedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");
        b.HasIndex(x => new { x.OrganizationId, x.ExpiresAtUtc }); b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
    }
}

public sealed class MobileTeamInvitationConfiguration : IEntityTypeConfiguration<MobileTeamInvitation>
{
    public void Configure(EntityTypeBuilder<MobileTeamInvitation> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Mobile).HasMaxLength(11).IsRequired();
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
        b.HasIndex(x => x.Mobile).IsUnique().HasFilter("\"AcceptedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");
        b.ToTable(t => {
            t.HasCheckConstraint("CK_MobileTeamInvitation_Name", "(\"FirstName\" IS NULL AND \"LastName\" IS NULL) OR (\"FirstName\" IS NOT NULL AND \"LastName\" IS NOT NULL AND length(\"FirstName\") > 0 AND length(\"LastName\") > 0 AND length(\"FirstName\") + 1 + length(\"LastName\") <= 200)");
            t.HasCheckConstraint("CK_MobileTeamInvitation_Mobile", "\"Mobile\" ~ '^09[0-9]{9}$'");
            t.HasCheckConstraint("CK_MobileTeamInvitation_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
            t.HasCheckConstraint("CK_MobileTeamInvitation_State", "\"AcceptedAtUtc\" IS NULL OR \"CancelledAtUtc\" IS NULL");
        });
    }
}
