using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewReportReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Report_Result",
                table: "InterviewEvaluationReports");

            migrationBuilder.CreateTable(
                name: "InterviewReviewEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClientOccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewReviewEntries", x => x.Id);
                    table.CheckConstraint("CK_Review_KindActor", "(\"ActorUserId\" IS NOT NULL AND \"Kind\" IN ('advance','review','reject') AND \"ClientOccurredAtUtc\" IS NULL) OR (\"ActorUserId\" IS NULL AND \"Kind\" IN ('consent_accepted','consent_revoked','microphone_disconnected','camera_disconnected','page_hidden','recording_started','recording_stopped') AND \"ClientOccurredAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InterviewReviewEntries_InterviewSessions_OrganizationId_Int~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewSessionRecordings",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    TimelineJson = table.Column<string>(type: "jsonb", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewSessionRecordings", x => x.InterviewSessionId);
                    table.CheckConstraint("CK_Recording_Values", "\"DurationSeconds\" BETWEEN 1 AND 14400 AND \"SizeBytes\" > 0 AND \"ContentType\" IN ('video/webm','video/mp4')");
                    table.ForeignKey(
                        name: "FK_InterviewSessionRecordings_InterviewSessions_OrganizationId~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Report_Result",
                table: "InterviewEvaluationReports",
                sql: "(\"ResultJson\" IS NULL AND \"Model\" IS NULL AND \"Provider\" IS NULL AND \"PromptVersion\" IS NULL AND \"CreatedAtUtc\" IS NULL) OR (\"ResultJson\" IS NOT NULL AND \"Model\" IS NOT NULL AND \"Provider\" IS NOT NULL AND \"PromptVersion\" IS NOT NULL AND \"CreatedAtUtc\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewReviewEntries_OrganizationId_InterviewSessionId_Re~",
                table: "InterviewReviewEntries",
                columns: new[] { "OrganizationId", "InterviewSessionId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessionRecordings_OrganizationId_InterviewSessionId",
                table: "InterviewSessionRecordings",
                columns: new[] { "OrganizationId", "InterviewSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "InterviewReviewEntries") OR EXISTS (SELECT 1 FROM "InterviewSessionRecordings")
                        OR EXISTS (SELECT 1 FROM "InterviewEvaluationReports" WHERE "ResultJson" IS NOT NULL AND "LeaseId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Interview review/recording history or active report regeneration exists; downgrade is unsafe.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "InterviewReviewEntries");

            migrationBuilder.DropTable(
                name: "InterviewSessionRecordings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Report_Result",
                table: "InterviewEvaluationReports");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Report_Result",
                table: "InterviewEvaluationReports",
                sql: "(\"ResultJson\" IS NULL AND \"Model\" IS NULL AND \"Provider\" IS NULL AND \"PromptVersion\" IS NULL AND \"CreatedAtUtc\" IS NULL) OR (\"ResultJson\" IS NOT NULL AND \"Model\" IS NOT NULL AND \"Provider\" IS NOT NULL AND \"PromptVersion\" IS NOT NULL AND \"CreatedAtUtc\" IS NOT NULL AND \"LeaseId\" IS NULL)");
        }
    }
}
