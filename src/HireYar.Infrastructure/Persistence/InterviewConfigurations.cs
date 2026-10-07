using HireYar.Domain.Interviews;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using HireYar.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class CandidateApplicationConfiguration : IEntityTypeConfiguration<CandidateApplication>
{
    public void Configure(EntityTypeBuilder<CandidateApplication> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.JobPositionId, x.Id });
        b.Property(x => x.FirstName).HasMaxLength(200).IsRequired(); b.Property(x => x.LastName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired(); b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.ResumeStorageKey).HasMaxLength(512);
        b.Property(x => x.ResumeContentType).HasMaxLength(100);
        b.Property(x => x.ResumeExtension).HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasOne<JobPosition>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobPositionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasIndex(x => new { x.OrganizationId, x.JobPositionId, x.CreatedAtUtc });
        b.ToTable(t => t.HasCheckConstraint("CK_Application_Status", "\"Status\" IN ('Invited','Interviewing','InterviewCompleted')"));
    }
}
public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
    }
}
public sealed class PositionSkillConfiguration : IEntityTypeConfiguration<PositionSkill>
{
    public void Configure(EntityTypeBuilder<PositionSkill> b)
    {
        b.HasKey(x => new { x.OrganizationId, x.JobPositionId, x.SkillId });
        b.HasOne<JobPosition>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobPositionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<Skill>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SkillId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.ToTable(t => { t.HasCheckConstraint("CK_PositionSkill_Priority", "\"Priority\" BETWEEN 0 AND 100"); t.HasCheckConstraint("CK_PositionSkill_Difficulty", "\"TargetDifficulty\" BETWEEN 1 AND 5"); });
    }
}
public sealed class BankQuestionConfiguration : IEntityTypeConfiguration<BankQuestion>
{
    public void Configure(EntityTypeBuilder<BankQuestion> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Text).HasMaxLength(2000).IsRequired(); b.Property(x => x.Topic).HasMaxLength(200).IsRequired();
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        b.HasOne<PositionSkill>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobPositionId, x.SkillId });
        b.HasIndex(x => new { x.OrganizationId, x.JobPositionId, x.IsActive });
        b.ToTable(t => t.HasCheckConstraint("CK_BankQuestion_Difficulty", "\"Difficulty\" BETWEEN 1 AND 5"));
    }
}
public sealed class QuestionAudioConfiguration : IEntityTypeConfiguration<QuestionAudio>
{
    public void Configure(EntityTypeBuilder<QuestionAudio> b)
    {
        b.HasKey(x => new { x.OrganizationId, x.AudioHash }); b.Property(x => x.AudioHash).HasMaxLength(64);
        b.Property(x => x.AudioStorageKey).HasMaxLength(512).IsRequired(); b.Property(x => x.AudioModel).HasMaxLength(100).IsRequired();
        b.Property(x => x.AudioVoice).HasMaxLength(100).IsRequired(); b.Property(x => x.AudioSpeed).HasPrecision(4, 2);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
    }
}
public sealed class InterviewSessionConfiguration : IEntityTypeConfiguration<InterviewSession>
{
    public void Configure(EntityTypeBuilder<InterviewSession> b)
    {
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        b.Property(x => x.AccessTokenHash).HasMaxLength(64).IsRequired(); b.HasIndex(x => x.AccessTokenHash).IsUnique();
        b.Property(x => x.RequiredCapabilityCode).HasMaxLength(64);
        b.Property(x => x.PositionTitleSnapshot).HasMaxLength(200);
        b.Property(x => x.PositionDescriptionSnapshot).HasMaxLength(10000);
        b.Property(x => x.TemplateTitleSnapshot).HasMaxLength(200);
        b.Property(x => x.WelcomeMessage).HasMaxLength(2000);
        b.Property(x => x.GoodbyeMessage).HasMaxLength(2000);
        b.Property(x => x.CandidateIntroductionMessage).HasMaxLength(2000);
        b.Property(x => x.CompanyIntroductionMessage).HasMaxLength(2000);
        b.Property(x => x.Language).HasMaxLength(20);
        b.Property(x => x.MasterPromptSnapshot).HasMaxLength(16000);
        b.Property(x => x.MasterPromptVersion).HasMaxLength(100);
        b.Property(x => x.ResumeTextSnapshot).HasMaxLength(32000);
        b.Property(x => x.CandidateIntroductionTranscript).HasMaxLength(32000);
        b.Property(x => x.Stage).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.CurrentInteractionMessage).HasMaxLength(2000);
        b.Ignore(x => x.RemainingInteractions);
        b.HasIndex(x => new { x.Status, x.NextRecoveryAtUtc, x.ProcessingLeaseExpiresAtUtc });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.CompletionReason).HasConversion<string>().HasMaxLength(30);
        b.Ignore(x => x.AnsweredQuestionCount);
        b.HasOne<CandidateApplication>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobPositionId, x.CandidateApplicationId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.JobPositionId, x.Id });
        b.HasOne<InterviewTemplate>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.JobPositionId, x.InterviewTemplateId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.JobPositionId, x.Id });
        b.HasIndex(x => new { x.OrganizationId, x.JobPositionId, x.Status });
        b.HasIndex(x => x.CandidateApplicationId);
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Interview_Stage", "\"Stage\" IN ('Welcome','CandidateIntroduction','Questions','CompanyIntroduction','Goodbye','Finished')");
            t.HasCheckConstraint("CK_Interview_Recovery", "\"RecoveryAttempts\" >= 0");
            t.HasCheckConstraint("CK_Interview_Interactions", "\"MaxInteractionsPerQuestion\" BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Interview_Status", "\"Status\" IN ('Created','WaitingForAnswer','ProcessingAnswer','Completed','Cancelled')");
            t.HasCheckConstraint("CK_Interview_Limits", "\"MaxQuestionCount\" BETWEEN 1 AND 30 AND \"MaxDurationSeconds\" BETWEEN 300 AND 3600");
            t.HasCheckConstraint("CK_Interview_Capability", "\"RequiredCapabilityCode\" IN ('hr_interview','technical_interview')");
        });
        b.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.InterviewSessionId);
        b.Navigation(x => x.Answers).HasField("answers").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.InterviewSessionId);
        b.Navigation(x => x.Questions).HasField("questions").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.Skills).WithOne().HasForeignKey(x => x.InterviewSessionId);
        b.Navigation(x => x.Skills).HasField("skills").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.SkillStates).WithOne().HasForeignKey(x => x.InterviewSessionId);
        b.Navigation(x => x.SkillStates).HasField("skillStates").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.CoveredTopics).WithOne().HasForeignKey(x => x.InterviewSessionId);
        b.Navigation(x => x.CoveredTopics).HasField("coveredTopics").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        b.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.InterviewSessionId);
        b.Navigation(x => x.Evidence).HasField("evidence").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}
public sealed class InterviewAnswerConfiguration : IEntityTypeConfiguration<InterviewAnswer>
{
    public void Configure(EntityTypeBuilder<InterviewAnswer> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasKey(x => x.Id); b.HasAlternateKey(x => new { x.InterviewSessionId, x.Id });
        b.HasIndex(x => new { x.InterviewSessionId, x.QuestionId }).IsUnique().HasFilter("NOT \"IsInteraction\"");
        b.Property(x => x.AssistantMessage).HasMaxLength(2000);
        b.Property(x => x.ClientTurnId).HasMaxLength(128);
        b.HasIndex(x => new { x.InterviewSessionId, x.ClientTurnId }).IsUnique().HasFilter("\"ClientTurnId\" IS NOT NULL");
        b.Property(x => x.VideoStorageKey).HasMaxLength(512); b.Property(x => x.VideoContentType).HasMaxLength(100);
        b.HasOne<InterviewQuestionSnapshot>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.QuestionId });
        b.Property(x => x.AudioStorageKey).HasMaxLength(512).IsRequired(); b.Property(x => x.AudioContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Transcript).HasMaxLength(32000); b.Property(x => x.TranscriptProvider).HasMaxLength(100);
        b.Property(x => x.TranscriptModel).HasMaxLength(100); b.Property(x => x.DecisionModel).HasMaxLength(100);
        b.Property(x => x.PromptVersion).HasMaxLength(100); b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.FailureStage).HasMaxLength(40); b.Property(x => x.SelectionSource).HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Answer_Status", "\"Status\" IN ('Uploaded','Transcribing','Transcribed','Evaluating','Processed','Failed')");
            t.HasCheckConstraint("CK_Answer_Interaction", "(NOT \"IsInteraction\" AND \"AssistantMessage\" IS NULL) OR (\"IsInteraction\" AND \"Status\" = 'Processed' AND \"AssistantMessage\" IS NOT NULL AND length(btrim(\"AssistantMessage\")) > 0)");
            t.HasCheckConstraint("CK_Answer_Size", "\"AudioSizeBytes\" > 0");
            t.HasCheckConstraint("CK_Answer_Video", "(\"VideoStorageKey\" IS NULL AND \"VideoContentType\" IS NULL AND \"VideoSizeBytes\" IS NULL) OR (\"VideoStorageKey\" IS NOT NULL AND \"VideoContentType\" IS NOT NULL AND \"VideoSizeBytes\" IS NOT NULL AND \"VideoSizeBytes\" > 0)");
            t.HasCheckConstraint("CK_Answer_BillableSeconds", "\"BillableSeconds\" BETWEEN 0 AND 120");
        });
    }
}
