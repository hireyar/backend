using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnswerReportAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnswerAssessments",
                columns: table => new
                {
                    AnswerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EvaluatorVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(7,6)", precision: 7, scale: 6, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerAssessments", x => x.AnswerId);
                    table.CheckConstraint("CK_AnswerAssessment_Lease", "(\"State\" = 'Processing' AND \"AttemptId\" IS NOT NULL AND \"LeaseExpiresAtUtc\" IS NOT NULL AND \"StartedAtUtc\" IS NOT NULL) OR (\"State\" <> 'Processing' AND \"AttemptId\" IS NULL AND \"LeaseExpiresAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_AnswerAssessment_Result", "jsonb_typeof(\"EvidenceJson\") = 'array' AND ((\"State\" = 'Succeeded' AND \"Score\" IS NOT NULL AND \"Score\" BETWEEN 0 AND 100 AND \"Confidence\" IS NOT NULL AND \"Confidence\" BETWEEN 0 AND 1 AND \"Explanation\" IS NOT NULL AND jsonb_array_length(\"EvidenceJson\") BETWEEN 1 AND 5) OR (\"State\" <> 'Succeeded' AND \"Score\" IS NULL AND \"Confidence\" IS NULL AND jsonb_array_length(\"EvidenceJson\") = 0))");
                    table.CheckConstraint("CK_AnswerAssessment_State", "\"State\" IN ('Queued','Processing','Succeeded','InsufficientEvidence','Failed','Unavailable') AND \"Attempts\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_AnswerAssessments_InterviewAnswers_InterviewSessionId_Answe~",
                        columns: x => new { x.InterviewSessionId, x.AnswerId },
                        principalTable: "InterviewAnswers",
                        principalColumns: new[] { "InterviewSessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnswerAssessments_InterviewSessions_OrganizationId_Intervie~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerAssessments_InterviewSessionId_AnswerId",
                table: "AnswerAssessments",
                columns: new[] { "InterviewSessionId", "AnswerId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerAssessments_OrganizationId_InterviewSessionId",
                table: "AnswerAssessments",
                columns: new[] { "OrganizationId", "InterviewSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerAssessments_State_NextAttemptAtUtc",
                table: "AnswerAssessments",
                columns: new[] { "State", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnswerAssessments");
        }
    }
}
