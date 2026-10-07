using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class InterviewIntroductionConfiguration : IEntityTypeConfiguration<InterviewIntroduction>
{
    public void Configure(EntityTypeBuilder<InterviewIntroduction> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => x.InterviewSessionId).IsUnique();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.InterviewSessionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();
        b.Property(x => x.ClientTurnId).HasMaxLength(128).IsRequired();
        b.Property(x => x.AudioStorageKey).HasMaxLength(512).IsRequired(); b.Property(x => x.AudioContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.VideoStorageKey).HasMaxLength(512); b.Property(x => x.VideoContentType).HasMaxLength(100);
        b.Property(x => x.Transcript).HasMaxLength(32000); b.Property(x => x.TranscriptProvider).HasMaxLength(100); b.Property(x => x.TranscriptModel).HasMaxLength(100);
        b.Property(x => x.FailureCode).HasMaxLength(100); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.Status, x.NextRecoveryAtUtc, x.ProcessingLeaseExpiresAtUtc });
        b.ToTable(t => {
            t.HasCheckConstraint("CK_Introduction_Status", "\"Status\" IN ('Uploaded','Transcribing','Processed','Failed')");
            t.HasCheckConstraint("CK_Introduction_Size", "\"AudioSizeBytes\" > 0 AND (\"DurationSeconds\" IS NULL OR \"DurationSeconds\" > 0)");
            t.HasCheckConstraint("CK_Introduction_Turn", "length(btrim(\"ClientTurnId\")) > 0");
            t.HasCheckConstraint("CK_Introduction_Recovery", "\"RecoveryAttempts\" >= 0");
            t.HasCheckConstraint("CK_Introduction_Transcript", "\"Status\" <> 'Processed' OR (\"Transcript\" IS NOT NULL AND length(btrim(\"Transcript\")) > 0 AND \"ProcessedAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Introduction_Lease", "(\"ProcessingLeaseId\" IS NULL AND \"ProcessingLeaseExpiresAtUtc\" IS NULL) OR (\"ProcessingLeaseId\" IS NOT NULL AND \"ProcessingLeaseExpiresAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Introduction_Video", "(\"VideoStorageKey\" IS NULL AND \"VideoContentType\" IS NULL AND \"VideoSizeBytes\" IS NULL) OR (\"VideoStorageKey\" IS NOT NULL AND \"VideoContentType\" IS NOT NULL AND \"VideoSizeBytes\" IS NOT NULL AND \"VideoSizeBytes\" > 0)");
        });
    }
}
