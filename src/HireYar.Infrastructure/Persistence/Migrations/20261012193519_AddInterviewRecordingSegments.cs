using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewRecordingSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewRecordingSegments",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentNumber = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    TimelineJson = table.Column<string>(type: "jsonb", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewRecordingSegments", x => new { x.InterviewSessionId, x.SegmentNumber });
                    table.CheckConstraint("CK_RecordingSegment_Values", "\"SegmentNumber\" BETWEEN 1 AND 1000 AND \"DurationSeconds\" BETWEEN 1 AND 14400 AND \"SizeBytes\" BETWEEN 1 AND 41943040 AND \"ContentType\" IN ('video/webm','video/mp4') AND length(\"ContentHash\") = 64");
                    table.ForeignKey(
                        name: "FK_InterviewRecordingSegments_InterviewSessions_OrganizationId~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewRecordingSegments_InterviewSessionId_UploadId",
                table: "InterviewRecordingSegments",
                columns: new[] { "InterviewSessionId", "UploadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewRecordingSegments_OrganizationId_InterviewSessionId",
                table: "InterviewRecordingSegments",
                columns: new[] { "OrganizationId", "InterviewSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "InterviewRecordingSegments") THEN
                        RAISE EXCEPTION 'Recording segments exist; downgrade is unsafe.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "InterviewRecordingSegments");
        }
    }
}
