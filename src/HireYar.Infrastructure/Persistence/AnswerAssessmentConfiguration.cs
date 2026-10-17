using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class AnswerAssessmentConfiguration : IEntityTypeConfiguration<AnswerReportAssessment>
{
    public void Configure(EntityTypeBuilder<AnswerReportAssessment> b)
    {
        b.HasKey(a => a.AnswerId); b.Property(a => a.AnswerId).ValueGeneratedNever();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(a => new { a.OrganizationId, a.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasOne<InterviewAnswer>().WithMany().HasForeignKey(a => new { a.InterviewSessionId, a.AnswerId })
            .HasPrincipalKey(a => new { a.InterviewSessionId, a.Id });
        b.HasIndex(a => new { a.State, a.NextAttemptAtUtc });
        b.HasIndex(a => new { a.OrganizationId, a.InterviewSessionId });
        b.Property(a => a.State).HasMaxLength(30); b.Property(a => a.FailureCode).HasMaxLength(64);
        b.Property(a => a.InputHash).HasMaxLength(64); b.Property(a => a.EvaluatorVersion).HasMaxLength(40);
        b.Property(a => a.Explanation).HasMaxLength(1500);
        b.Property(a => a.Provider).HasMaxLength(100); b.Property(a => a.Model).HasMaxLength(100);
        b.Property(a => a.Score).HasPrecision(7, 4); b.Property(a => a.Confidence).HasPrecision(7, 6);
        b.Property(a => a.DimensionFailureCode).HasMaxLength(64);
        b.ToTable(t => {
            t.HasCheckConstraint("CK_AnswerAssessment_DimensionAttempts", "\"DimensionAttempts\" BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_AnswerAssessment_DimensionLease", "(\"DimensionAttemptId\" IS NULL) = (\"DimensionLeaseExpiresAtUtc\" IS NULL)");
        });
        b.Property(a => a.DimensionsJson).HasColumnType("jsonb").HasDefaultValue("[]");
        b.Property(a => a.EvidenceJson).HasColumnType("jsonb");
        b.ToTable(t => {
            t.HasCheckConstraint("CK_AnswerAssessment_State", "\"State\" IN ('Queued','Processing','Succeeded','InsufficientEvidence','Failed','Unavailable') AND \"Attempts\" BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_AnswerAssessment_Lease", "(\"State\" = 'Processing' AND \"AttemptId\" IS NOT NULL AND \"LeaseExpiresAtUtc\" IS NOT NULL AND \"StartedAtUtc\" IS NOT NULL) OR (\"State\" <> 'Processing' AND \"AttemptId\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL)");
            t.HasCheckConstraint("CK_AnswerAssessment_Result", "jsonb_typeof(\"EvidenceJson\") = 'array' AND ((\"State\" = 'Succeeded' AND \"Score\" IS NOT NULL AND \"Score\" BETWEEN 0 AND 100 AND \"Confidence\" IS NOT NULL AND \"Confidence\" BETWEEN 0 AND 1 AND \"Explanation\" IS NOT NULL AND jsonb_array_length(\"EvidenceJson\") BETWEEN 1 AND 5) OR (\"State\" <> 'Succeeded' AND \"Score\" IS NULL AND \"Confidence\" IS NULL AND jsonb_array_length(\"EvidenceJson\") = 0))");
        });
    }
}
