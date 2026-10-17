using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class SessionReportNarrativeConfiguration : IEntityTypeConfiguration<SessionReportNarrative>
{
    public void Configure(EntityTypeBuilder<SessionReportNarrative> b)
    {
        b.HasKey(x=>new{x.InterviewSessionId,x.InputHash});
        b.Property(x=>x.InputHash).HasMaxLength(64);b.Property(x=>x.ReportFingerprint).HasMaxLength(64);
        b.Property(x=>x.State).HasMaxLength(20);b.Property(x=>x.FailureCode).HasMaxLength(64);
        b.Property(x=>x.Provider).HasMaxLength(100);b.Property(x=>x.Model).HasMaxLength(100);
        b.Property(x=>x.ResultJson).HasColumnType("jsonb");
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(x=>new{x.OrganizationId,x.InterviewSessionId}).HasPrincipalKey(s=>new{s.OrganizationId,s.Id});
        b.HasIndex(x=>new{x.OrganizationId,x.InterviewSessionId});
        b.ToTable(t=> {
            t.HasCheckConstraint("CK_ReportNarrative_State","\"State\" IN ('Processing','Succeeded','Failed') AND \"Attempts\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_ReportNarrative_Lease","(\"State\" = 'Processing' AND \"AttemptId\" IS NOT NULL AND \"LeaseExpiresAtUtc\" IS NOT NULL) OR (\"State\" <> 'Processing' AND \"AttemptId\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL)");
            t.HasCheckConstraint("CK_ReportNarrative_Result","jsonb_typeof(\"ResultJson\") = 'object'");
        });
    }
}