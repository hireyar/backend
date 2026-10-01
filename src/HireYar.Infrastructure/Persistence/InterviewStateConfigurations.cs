using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HireYar.Infrastructure.Persistence;

public sealed class QuestionSnapshotConfiguration : IEntityTypeConfiguration<InterviewQuestionSnapshot>
{
    public void Configure(EntityTypeBuilder<InterviewQuestionSnapshot> b)
    {
        b.ToTable("InterviewQuestionSnapshots"); b.HasKey(x => new { x.InterviewSessionId, x.QuestionId });
        b.Property(x => x.Text).HasMaxLength(2000).IsRequired(); b.Property(x => x.Topic).HasMaxLength(200).IsRequired();
        b.HasOne<InterviewSkill>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.SkillId });
    }
}
public sealed class SkillSnapshotConfiguration : IEntityTypeConfiguration<InterviewSkill>
{
    public void Configure(EntityTypeBuilder<InterviewSkill> b)
    {
        b.ToTable("InterviewSkillSnapshots"); b.HasKey(x => new { x.InterviewSessionId, x.SkillId });
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}
public sealed class SkillStateConfiguration : IEntityTypeConfiguration<CandidateSkillState>
{
    public void Configure(EntityTypeBuilder<CandidateSkillState> b)
    {
        b.ToTable("CandidateSkillStates", t => t.HasCheckConstraint("CK_SkillState_Ranges", "\"CurrentScore\" BETWEEN 0 AND 1 AND \"Confidence\" BETWEEN 0 AND 1 AND \"EvidenceCount\" >= 0"));
        b.HasKey(x => new { x.InterviewSessionId, x.SkillId });
        b.Property(x => x.CurrentScore).HasPrecision(6, 5); b.Property(x => x.Confidence).HasPrecision(6, 5);
        b.HasOne<InterviewSkill>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.SkillId });
    }
}
public sealed class CoveredTopicConfiguration : IEntityTypeConfiguration<InterviewCoveredTopic>
{
    public void Configure(EntityTypeBuilder<InterviewCoveredTopic> b)
    {
        b.ToTable("InterviewCoveredTopics", t => t.HasCheckConstraint("CK_Topic_Coverage", "\"Coverage\" BETWEEN 0 AND 1 AND \"EvidenceCount\" >= 0"));
        b.HasKey(x => new { x.InterviewSessionId, x.SkillId, x.Topic });
        b.Property(x => x.Topic).HasMaxLength(200); b.Property(x => x.Coverage).HasPrecision(6, 5);
        b.HasOne<InterviewSkill>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.SkillId });
    }
}
public sealed class SkillEvidenceConfiguration : IEntityTypeConfiguration<SkillEvidence>
{
    public void Configure(EntityTypeBuilder<SkillEvidence> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable("CandidateSkillEvidence", t => t.HasCheckConstraint("CK_Evidence_Ranges", "\"ScoreBefore\" BETWEEN 0 AND 1 AND \"ScoreAfter\" BETWEEN 0 AND 1 AND \"Confidence\" BETWEEN 0 AND 1 AND \"Delta\" BETWEEN -1 AND 1"));
        b.HasKey(x => x.Id); b.Property(x => x.Evidence).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ScoreBefore).HasPrecision(6, 5); b.Property(x => x.ScoreAfter).HasPrecision(6, 5);
        b.Property(x => x.Confidence).HasPrecision(6, 5); b.Property(x => x.Delta).HasPrecision(6, 5);
        b.HasOne<InterviewAnswer>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.InterviewAnswerId }).HasPrincipalKey(x => new { x.InterviewSessionId, x.Id });
        b.HasOne<InterviewSkill>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.SkillId });
        b.HasOne<InterviewQuestionSnapshot>().WithMany().HasForeignKey(x => new { x.InterviewSessionId, x.QuestionId });
        b.HasIndex(x => new { x.InterviewSessionId, x.SkillId });
    }
}
