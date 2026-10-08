using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class InterviewEvaluationReportConfiguration : IEntityTypeConfiguration<InterviewEvaluationReport>
{
    public void Configure(EntityTypeBuilder<InterviewEvaluationReport> b)
    {
        b.HasKey(r => r.InterviewSessionId); b.Property(r => r.InterviewSessionId).ValueGeneratedNever();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(r => new { r.OrganizationId, r.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.Property(r => r.ResultJson).HasColumnType("jsonb");
        b.Property(r => r.Model).HasMaxLength(100); b.Property(r => r.Provider).HasMaxLength(100);
        b.Property(r => r.PromptVersion).HasMaxLength(40);
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Report_Lease", "(\"LeaseId\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL) OR (\"LeaseId\" IS NOT NULL AND \"LeaseExpiresAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Report_Result", "(\"ResultJson\" IS NULL AND \"Model\" IS NULL AND \"Provider\" IS NULL AND \"PromptVersion\" IS NULL AND \"CreatedAtUtc\" IS NULL) OR (\"ResultJson\" IS NOT NULL AND \"Model\" IS NOT NULL AND \"Provider\" IS NOT NULL AND \"PromptVersion\" IS NOT NULL AND \"CreatedAtUtc\" IS NOT NULL AND \"LeaseId\" IS NULL)");
        });
    }
}
