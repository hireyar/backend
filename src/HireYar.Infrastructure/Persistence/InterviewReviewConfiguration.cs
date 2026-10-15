using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class InterviewReviewConfiguration : IEntityTypeConfiguration<InterviewReviewEntry>
{
    public void Configure(EntityTypeBuilder<InterviewReviewEntry> b)
    {
        b.HasKey(e => e.Id);
        b.HasIndex(e => new { e.OrganizationId, e.InterviewSessionId, e.RequestId }).IsUnique();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(e => new { e.OrganizationId, e.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.Property(e => e.ReportSnapshotJson).HasColumnType("jsonb");
        b.Property(e => e.ReportFingerprint).HasMaxLength(64);
        b.Property(e => e.Kind).HasMaxLength(40); b.Property(e => e.Note).HasMaxLength(2000);
        b.ToTable(t => t.HasCheckConstraint("CK_Review_KindActor",
            "(\"ActorUserId\" IS NOT NULL AND \"Kind\" IN ('advance','review','reject') AND \"ClientOccurredAtUtc\" IS NULL) OR (\"ActorUserId\" IS NULL AND \"Kind\" IN ('consent_accepted','consent_revoked','microphone_disconnected','camera_disconnected','page_hidden','recording_started','recording_stopped') AND \"ClientOccurredAtUtc\" IS NOT NULL)"));
    }
}
public sealed class InterviewSessionRecordingConfiguration : IEntityTypeConfiguration<InterviewSessionRecording>
{
    public void Configure(EntityTypeBuilder<InterviewSessionRecording> b)
    {
        b.HasKey(r => r.InterviewSessionId); b.Property(r => r.InterviewSessionId).ValueGeneratedNever();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(r => new { r.OrganizationId, r.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.Property(r => r.StorageKey).HasMaxLength(512); b.Property(r => r.ContentType).HasMaxLength(100);
        b.Property(r => r.TimelineJson).HasColumnType("jsonb");
        b.ToTable(t => t.HasCheckConstraint("CK_Recording_Values", "\"DurationSeconds\" BETWEEN 1 AND 14400 AND \"SizeBytes\" > 0 AND \"ContentType\" IN ('video/webm','video/mp4')"));
    }
}

public sealed class InterviewRecordingSegmentConfiguration : IEntityTypeConfiguration<InterviewRecordingSegment>
{
    public void Configure(EntityTypeBuilder<InterviewRecordingSegment> b)
    {
        b.HasKey(r => new { r.InterviewSessionId, r.SegmentNumber });
        b.HasIndex(r => new { r.InterviewSessionId, r.UploadId }).IsUnique();
        b.HasOne<InterviewSession>().WithMany().HasForeignKey(r => new { r.OrganizationId, r.InterviewSessionId })
            .HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.Property(r => r.StorageKey).HasMaxLength(512); b.Property(r => r.ContentType).HasMaxLength(100);
        b.Property(r => r.ContentHash).HasMaxLength(64); b.Property(r => r.TimelineJson).HasColumnType("jsonb");
        b.ToTable(t => t.HasCheckConstraint("CK_RecordingSegment_Values",
            "\"SegmentNumber\" BETWEEN 1 AND 1000 AND \"DurationSeconds\" BETWEEN 1 AND 14400 AND \"SizeBytes\" BETWEEN 1 AND 41943040 AND \"ContentType\" IN ('video/webm','video/mp4') AND length(\"ContentHash\") = 64"));
    }
}
