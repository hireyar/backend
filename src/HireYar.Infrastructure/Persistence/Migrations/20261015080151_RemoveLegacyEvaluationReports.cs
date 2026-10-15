using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyEvaluationReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewEvaluationReports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewEvaluationReports",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FailureStage = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GenerationAttempts = table.Column<int>(type: "integer", nullable: false),
                    GenerationSettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    GenerationStage = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewEvaluationReports", x => x.InterviewSessionId);
                    table.CheckConstraint("CK_Report_Generation", "\"GenerationStage\" IN ('Queued', 'Generating', 'Validating', 'Saving', 'Succeeded', 'Failed') AND \"GenerationAttempts\" >= 0");
                    table.CheckConstraint("CK_Report_Lease", "(\"LeaseId\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL) OR (\"LeaseId\" IS NOT NULL AND \"LeaseExpiresAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Report_Result", "(\"ResultJson\" IS NULL AND \"Model\" IS NULL AND \"Provider\" IS NULL AND \"PromptVersion\" IS NULL AND \"CreatedAtUtc\" IS NULL) OR (\"ResultJson\" IS NOT NULL AND \"Model\" IS NOT NULL AND \"Provider\" IS NOT NULL AND \"PromptVersion\" IS NOT NULL AND \"CreatedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InterviewEvaluationReports_InterviewSessions_OrganizationId~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewEvaluationReports_GenerationStage_RequestedAtUtc",
                table: "InterviewEvaluationReports",
                columns: new[] { "GenerationStage", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewEvaluationReports_OrganizationId_InterviewSessionId",
                table: "InterviewEvaluationReports",
                columns: new[] { "OrganizationId", "InterviewSessionId" });
        }
    }
}
