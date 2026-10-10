using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressiveTopicQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ProgressiveTopicQuestions",
                table: "InterviewSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AssessmentConfidence",
                table: "InterviewAnswers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Correctness",
                table: "InterviewAnswers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_TopicPolicy",
                table: "InterviewSessions",
                sql: "NOT (\"AskOneQuestionPerTopic\" AND \"ProgressiveTopicQuestions\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Answer_Assessment",
                table: "InterviewAnswers",
                sql: "(\"Correctness\" IS NULL AND \"AssessmentConfidence\" IS NULL) OR (\"Correctness\" IS NOT NULL AND \"AssessmentConfidence\" IS NOT NULL AND \"Correctness\" BETWEEN 0 AND 1 AND \"AssessmentConfidence\" BETWEEN 0 AND 1 AND \"Status\" = 'Processed' AND NOT \"IsInteraction\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "InterviewSessions" WHERE "ProgressiveTopicQuestions")
                     OR EXISTS (SELECT 1 FROM "InterviewAnswers" WHERE "Correctness" IS NOT NULL OR "AssessmentConfidence" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot remove progressive topic policy or persisted answer assessments.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_TopicPolicy",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Answer_Assessment",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "ProgressiveTopicQuestions",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "AssessmentConfidence",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "Correctness",
                table: "InterviewAnswers");
        }
    }
}
