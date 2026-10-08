using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewEvaluationReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewEvaluationReports",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewEvaluationReports", x => x.InterviewSessionId);
                    table.CheckConstraint("CK_Report_Lease", "(\"LeaseId\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL) OR (\"LeaseId\" IS NOT NULL AND \"LeaseExpiresAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Report_Result", "(\"ResultJson\" IS NULL AND \"Model\" IS NULL AND \"Provider\" IS NULL AND \"PromptVersion\" IS NULL AND \"CreatedAtUtc\" IS NULL) OR (\"ResultJson\" IS NOT NULL AND \"Model\" IS NOT NULL AND \"Provider\" IS NOT NULL AND \"PromptVersion\" IS NOT NULL AND \"CreatedAtUtc\" IS NOT NULL AND \"LeaseId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_InterviewEvaluationReports_InterviewSessions_OrganizationId~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewEvaluationReports_OrganizationId_InterviewSessionId",
                table: "InterviewEvaluationReports",
                columns: new[] { "OrganizationId", "InterviewSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "InterviewEvaluationReports" WHERE "ResultJson" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Evaluation report history exists; downgrade would discard persisted reports.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "InterviewEvaluationReports");
        }
    }
}
