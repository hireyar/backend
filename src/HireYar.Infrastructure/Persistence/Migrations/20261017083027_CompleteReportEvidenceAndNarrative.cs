using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteReportEvidenceAndNarrative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DimensionAttemptId",
                table: "AnswerAssessments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DimensionAttempts",
                table: "AnswerAssessments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DimensionFailureCode",
                table: "AnswerAssessments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DimensionLeaseExpiresAtUtc",
                table: "AnswerAssessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SessionReportNarratives",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionReportNarratives", x => new { x.InterviewSessionId, x.InputHash });
                    table.CheckConstraint("CK_ReportNarrative_Lease", "(\"State\" = 'Processing' AND \"AttemptId\" IS NOT NULL AND \"LeaseExpiresAtUtc\" IS NOT NULL) OR (\"State\" <> 'Processing' AND \"AttemptId\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_ReportNarrative_Result", "jsonb_typeof(\"ResultJson\") = 'object'");
                    table.CheckConstraint("CK_ReportNarrative_State", "\"State\" IN ('Processing','Succeeded','Failed') AND \"Attempts\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_SessionReportNarratives_InterviewSessions_OrganizationId_In~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnswerAssessment_DimensionAttempts",
                table: "AnswerAssessments",
                sql: "\"DimensionAttempts\" BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnswerAssessment_DimensionLease",
                table: "AnswerAssessments",
                sql: "(\"DimensionAttemptId\" IS NULL) = (\"DimensionLeaseExpiresAtUtc\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_SessionReportNarratives_OrganizationId_InterviewSessionId",
                table: "SessionReportNarratives",
                columns: new[] { "OrganizationId", "InterviewSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "SessionReportNarratives") OR
                     EXISTS (SELECT 1 FROM "AnswerAssessments" WHERE "DimensionAttempts" > 0) THEN
                    RAISE EXCEPTION 'Report narrative or dimension recovery audit exists; downgrade would erase data' USING ERRCODE = 'P0001';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "SessionReportNarratives");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnswerAssessment_DimensionAttempts",
                table: "AnswerAssessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnswerAssessment_DimensionLease",
                table: "AnswerAssessments");

            migrationBuilder.DropColumn(
                name: "DimensionAttemptId",
                table: "AnswerAssessments");

            migrationBuilder.DropColumn(
                name: "DimensionAttempts",
                table: "AnswerAssessments");

            migrationBuilder.DropColumn(
                name: "DimensionFailureCode",
                table: "AnswerAssessments");

            migrationBuilder.DropColumn(
                name: "DimensionLeaseExpiresAtUtc",
                table: "AnswerAssessments");
        }
    }
}
