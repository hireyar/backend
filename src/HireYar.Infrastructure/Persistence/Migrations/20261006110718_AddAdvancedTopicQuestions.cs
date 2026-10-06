using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedTopicQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseTopicQuestions",
                table: "InterviewTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AskOneQuestionPerTopic",
                table: "InterviewSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "InterviewQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "TopicName",
                table: "InterviewQuestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TopicOrder",
                table: "InterviewQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuestions_InterviewTemplateId_TopicOrder_Difficulty",
                table: "InterviewQuestions",
                columns: new[] { "InterviewTemplateId", "TopicOrder", "Difficulty" },
                unique: true,
                filter: "\"TopicOrder\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Question_Topic",
                table: "InterviewQuestions",
                sql: "(\"TopicOrder\" IS NULL AND \"TopicName\" IS NULL AND \"Difficulty\" = 3) OR (\"TopicOrder\" IS NOT NULL AND \"TopicOrder\" BETWEEN 1 AND 10 AND \"TopicName\" IS NOT NULL AND length(trim(\"TopicName\")) > 0 AND \"Difficulty\" IN (1,3,5))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InterviewQuestions_InterviewTemplateId_TopicOrder_Difficulty",
                table: "InterviewQuestions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Question_Topic",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "UseTopicQuestions",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "AskOneQuestionPerTopic",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "TopicName",
                table: "InterviewQuestions");

            migrationBuilder.DropColumn(
                name: "TopicOrder",
                table: "InterviewQuestions");
        }
    }
}
