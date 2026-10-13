using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportGenerationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "FailureStage", table: "InterviewEvaluationReports",
                type: "character varying(24)", maxLength: 24, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "InterviewEvaluationReports",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinishedAtUtc",
                table: "InterviewEvaluationReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenerationAttempts",
                table: "InterviewEvaluationReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GenerationSettingsJson",
                table: "InterviewEvaluationReports",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationStage",
                table: "InterviewEvaluationReports",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Failed");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedAtUtc",
                table: "InterviewEvaluationReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                table: "InterviewEvaluationReports",
                type: "timestamp with time zone",
                nullable: true);

            // Preserve cached reports and identify interrupted legacy requests without rerunning AI.
            migrationBuilder.Sql("""
                UPDATE "InterviewEvaluationReports"
                SET "GenerationStage" = CASE
                    WHEN "LeaseId" IS NOT NULL THEN 'Generating'
                    WHEN "ResultJson" IS NOT NULL THEN 'Succeeded' ELSE 'Failed' END,
                    "GenerationAttempts" = CASE WHEN "LeaseId" IS NOT NULL OR "ResultJson" IS NOT NULL THEN 1 ELSE 0 END,
                    "RequestedAtUtc" = "CreatedAtUtc",
                    "FinishedAtUtc" = CASE WHEN "LeaseId" IS NULL THEN "CreatedAtUtc" ELSE NULL END,
                    "FailureCode" = CASE WHEN "LeaseId" IS NULL AND "ResultJson" IS NULL THEN 'report_not_generated' ELSE NULL END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewEvaluationReports_GenerationStage_RequestedAtUtc",
                table: "InterviewEvaluationReports",
                columns: new[] { "GenerationStage", "RequestedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Report_Generation",
                table: "InterviewEvaluationReports",
                sql: "\"GenerationStage\" IN ('Queued', 'Generating', 'Validating', 'Saving', 'Succeeded', 'Failed') AND \"GenerationAttempts\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "InterviewEvaluationReports" WHERE "GenerationStage" <> 'Failed' OR "GenerationAttempts" > 0 OR "RequestedAtUtc" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot remove persisted report generation history or pending work.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(name: "FailureStage", table: "InterviewEvaluationReports");
            migrationBuilder.DropIndex(
                name: "IX_InterviewEvaluationReports_GenerationStage_RequestedAtUtc",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Report_Generation",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropColumn(
                name: "FinishedAtUtc",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropColumn(
                name: "GenerationAttempts",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropColumn(
                name: "GenerationSettingsJson",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropColumn(
                name: "GenerationStage",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropColumn(
                name: "RequestedAtUtc",
                table: "InterviewEvaluationReports");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "InterviewEvaluationReports");
        }
    }
}
