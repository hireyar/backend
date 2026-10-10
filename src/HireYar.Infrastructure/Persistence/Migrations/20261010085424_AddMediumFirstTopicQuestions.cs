using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediumFirstTopicQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MediumFirstTopicQuestions",
                table: "InterviewSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_MediumFirst",
                table: "InterviewSessions",
                sql: "NOT \"MediumFirstTopicQuestions\" OR (\"ProgressiveTopicQuestions\" AND \"MaxQuestionCount\" BETWEEN 10 AND 20 AND \"MaxQuestionCount\" % 2 = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "InterviewSessions" WHERE "MediumFirstTopicQuestions") THEN
                    RAISE EXCEPTION 'Cannot remove medium-first policy associated with persisted answer assessments; downgrade is unsafe.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_MediumFirst",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "MediumFirstTopicQuestions",
                table: "InterviewSessions");
        }
    }
}
