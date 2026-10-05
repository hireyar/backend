using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPanelWizard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoodbyeMessage",
                table: "InterviewTemplates",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "از وقتی که گذاشتید متشکریم.");

            migrationBuilder.AddColumn<string>(
                name: "InterviewType",
                table: "InterviewTemplates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "general");

            migrationBuilder.AddColumn<string>(
                name: "WelcomeMessage",
                table: "InterviewTemplates",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "به مصاحبه خوش آمدید.");

            migrationBuilder.AddColumn<string>(
                name: "GoodbyeMessage",
                table: "InterviewSessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionDescriptionSnapshot",
                table: "InterviewSessions",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionTitleSnapshot",
                table: "InterviewSessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateTitleSnapshot",
                table: "InterviewSessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WelcomeMessage",
                table: "InterviewSessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "InterviewQuestionSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResumeContentType",
                table: "CandidateApplications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeExtension",
                table: "CandidateApplications",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeStorageKey",
                table: "CandidateApplications",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Template_Type",
                table: "InterviewTemplates",
                sql: "\"InterviewType\" IN ('general','specialized')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Template_Type",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "GoodbyeMessage",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "InterviewType",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "WelcomeMessage",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "GoodbyeMessage",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "PositionDescriptionSnapshot",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "PositionTitleSnapshot",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "TemplateTitleSnapshot",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "WelcomeMessage",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "InterviewQuestionSnapshots");

            migrationBuilder.DropColumn(
                name: "ResumeContentType",
                table: "CandidateApplications");

            migrationBuilder.DropColumn(
                name: "ResumeExtension",
                table: "CandidateApplications");

            migrationBuilder.DropColumn(
                name: "ResumeStorageKey",
                table: "CandidateApplications");
        }
    }
}
