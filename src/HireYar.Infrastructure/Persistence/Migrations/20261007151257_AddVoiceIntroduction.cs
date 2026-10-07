using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceIntroduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewIntroductions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientTurnId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AudioStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AudioContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AudioSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    VideoStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    VideoContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VideoSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    LastSpeechEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Transcript = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    TranscriptProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TranscriptModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProcessingLeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessingLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRecoveryAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecoveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewIntroductions", x => x.Id);
                    table.CheckConstraint("CK_Introduction_Lease", "(\"ProcessingLeaseId\" IS NULL AND \"ProcessingLeaseExpiresAtUtc\" IS NULL) OR (\"ProcessingLeaseId\" IS NOT NULL AND \"ProcessingLeaseExpiresAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Introduction_Recovery", "\"RecoveryAttempts\" >= 0");
                    table.CheckConstraint("CK_Introduction_Size", "\"AudioSizeBytes\" > 0 AND (\"DurationSeconds\" IS NULL OR \"DurationSeconds\" > 0)");
                    table.CheckConstraint("CK_Introduction_Status", "\"Status\" IN ('Uploaded','Transcribing','Processed','Failed')");
                    table.CheckConstraint("CK_Introduction_Transcript", "\"Status\" <> 'Processed' OR (\"Transcript\" IS NOT NULL AND length(btrim(\"Transcript\")) > 0 AND \"ProcessedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Introduction_Turn", "length(btrim(\"ClientTurnId\")) > 0");
                    table.CheckConstraint("CK_Introduction_Video", "(\"VideoStorageKey\" IS NULL AND \"VideoContentType\" IS NULL AND \"VideoSizeBytes\" IS NULL) OR (\"VideoStorageKey\" IS NOT NULL AND \"VideoContentType\" IS NOT NULL AND \"VideoSizeBytes\" IS NOT NULL AND \"VideoSizeBytes\" > 0)");
                    table.ForeignKey(
                        name: "FK_InterviewIntroductions_InterviewSessions_OrganizationId_Int~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewIntroductions_InterviewSessionId",
                table: "InterviewIntroductions",
                column: "InterviewSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewIntroductions_OrganizationId_InterviewSessionId",
                table: "InterviewIntroductions",
                columns: new[] { "OrganizationId", "InterviewSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewIntroductions_Status_NextRecoveryAtUtc_ProcessingL~",
                table: "InterviewIntroductions",
                columns: new[] { "Status", "NextRecoveryAtUtc", "ProcessingLeaseExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "InterviewIntroductions") THEN
                        RAISE EXCEPTION 'Voice introduction history exists; downgrade would discard captured media and transcript metadata.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "InterviewIntroductions");
        }
    }
}
