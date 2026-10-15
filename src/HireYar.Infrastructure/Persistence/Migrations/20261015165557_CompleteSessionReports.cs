using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSessionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ReportSnapshotJson", table: "InterviewReviewEntries", type: "jsonb", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportFingerprint",
                table: "InterviewReviewEntries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DimensionsJson",
                table: "AnswerAssessments",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "SessionReportPolicies",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    PassingScore = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    StrongScore = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    MinimumCoverage = table.Column<decimal>(type: "numeric(7,6)", precision: 7, scale: 6, nullable: false),
                    WeightsJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionReportPolicies", x => x.InterviewSessionId);
                    table.CheckConstraint("CK_ReportPolicy_Thresholds", "\"PassingScore\" BETWEEN 0 AND \"StrongScore\" AND \"StrongScore\" <= 100 AND \"MinimumCoverage\" > 0 AND \"MinimumCoverage\" <= 1");
                    table.CheckConstraint("CK_ReportPolicy_Weights", "jsonb_typeof(\"WeightsJson\") = 'array'");
                    table.ForeignKey(
                        name: "FK_SessionReportPolicies_InterviewSessions_OrganizationId_Inte~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionReportPolicies_OrganizationId_InterviewSessionId",
                table: "SessionReportPolicies",
                columns: new[] { "OrganizationId", "InterviewSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "SessionReportPolicies") OR
                     EXISTS (SELECT 1 FROM "InterviewReviewEntries" WHERE "ReportFingerprint" IS NOT NULL OR "ReportSnapshotJson" IS NOT NULL) OR
                     EXISTS (SELECT 1 FROM "AnswerAssessments" WHERE "DimensionsJson" <> '[]'::jsonb) THEN
                    RAISE EXCEPTION 'Report evidence or review policy exists; downgrade would erase audit data' USING ERRCODE = 'P0001';
                  END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "SessionReportPolicies");

            migrationBuilder.DropColumn(name: "ReportSnapshotJson", table: "InterviewReviewEntries");

            migrationBuilder.DropColumn(
                name: "ReportFingerprint",
                table: "InterviewReviewEntries");

            migrationBuilder.DropColumn(
                name: "DimensionsJson",
                table: "AnswerAssessments");
        }
    }
}
