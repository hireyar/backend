using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class SessionReportPolicyConfiguration : IEntityTypeConfiguration<SessionReportPolicy>
{
    public void Configure(EntityTypeBuilder<SessionReportPolicy> b)
    {
        b.HasKey(p => p.InterviewSessionId); b.Property(p => p.InterviewSessionId).ValueGeneratedNever();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(p => new { p.OrganizationId, p.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.Property(p => p.PassingScore).HasPrecision(7,4); b.Property(p => p.StrongScore).HasPrecision(7,4);
        b.Property(p => p.MinimumCoverage).HasPrecision(7,6); b.Property(p => p.WeightsJson).HasColumnType("jsonb");
        b.ToTable(t => {
            t.HasCheckConstraint("CK_ReportPolicy_Thresholds", "\"PassingScore\" BETWEEN 0 AND \"StrongScore\" AND \"StrongScore\" <= 100 AND \"MinimumCoverage\" > 0 AND \"MinimumCoverage\" <= 1");
            t.HasCheckConstraint("CK_ReportPolicy_Weights", "jsonb_typeof(\"WeightsJson\") = 'array'");
        });
    }
}
