using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewContextAndRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_QuestionId",
                table: "InterviewAnswers");

            migrationBuilder.AddColumn<bool>(
                name: "CandidateIntroductionEnabled",
                table: "InterviewTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CandidateIntroductionMessage",
                table: "InterviewTemplates",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "قبل از شروع سؤال‌ها، لطفاً خودتون رو معرفی کنید و درباره سوابق کاری و پروژه‌هایی که روی آن‌ها کار کردین برای ما توضیح بدین");

            migrationBuilder.AddColumn<bool>(
                name: "CompanyIntroductionEnabled",
                table: "InterviewTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CompanyIntroductionMessage",
                table: "InterviewTemplates",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CandidateIntroductionEnabled",
                table: "InterviewSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CandidateIntroductionMessage",
                table: "InterviewSessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CandidateIntroductionTranscript",
                table: "InterviewSessions",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompanyIntroductionEnabled",
                table: "InterviewSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CompanyIntroductionMessage",
                table: "InterviewSessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentInteractionMessage",
                table: "InterviewSessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "InterviewSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "fa-IR");

            migrationBuilder.AddColumn<string>(
                name: "MasterPromptSnapshot",
                table: "InterviewSessions",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MasterPromptVersion",
                table: "InterviewSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxInteractionsPerQuestion",
                table: "InterviewSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRecoveryAtUtc",
                table: "InterviewSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecoveryAttempts",
                table: "InterviewSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResumeTextSnapshot",
                table: "InterviewSessions",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "InterviewSessions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Questions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StageChangedAtUtc",
                table: "InterviewSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssistantMessage",
                table: "InterviewAnswers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInteraction",
                table: "InterviewAnswers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE \"InterviewSessions\" SET \"Stage\" = 'Finished' WHERE \"Status\" IN ('Completed','Cancelled');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Template_CandidateIntroduction",
                table: "InterviewTemplates",
                sql: "NOT \"CandidateIntroductionEnabled\" OR length(btrim(\"CandidateIntroductionMessage\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Template_CompanyIntroduction",
                table: "InterviewTemplates",
                sql: "NOT \"CompanyIntroductionEnabled\" OR length(btrim(\"CompanyIntroductionMessage\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_Status_NextRecoveryAtUtc_ProcessingLeaseE~",
                table: "InterviewSessions",
                columns: new[] { "Status", "NextRecoveryAtUtc", "ProcessingLeaseExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_Interactions",
                table: "InterviewSessions",
                sql: "\"MaxInteractionsPerQuestion\" BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_Recovery",
                table: "InterviewSessions",
                sql: "\"RecoveryAttempts\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_Stage",
                table: "InterviewSessions",
                sql: "\"Stage\" IN ('Welcome','CandidateIntroduction','Questions','CompanyIntroduction','Goodbye','Finished')");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_QuestionId",
                table: "InterviewAnswers",
                columns: new[] { "InterviewSessionId", "QuestionId" },
                unique: true,
                filter: "NOT \"IsInteraction\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Answer_Interaction",
                table: "InterviewAnswers",
                sql: "(NOT \"IsInteraction\" AND \"AssistantMessage\" IS NULL) OR (\"IsInteraction\" AND \"Status\" = 'Processed' AND \"AssistantMessage\" IS NOT NULL AND length(btrim(\"AssistantMessage\")) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"InterviewAnswers\" WHERE \"IsInteraction\") THEN RAISE EXCEPTION 'Cannot downgrade while persisted interview interactions exist'; END IF; END $$;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Template_CandidateIntroduction",
                table: "InterviewTemplates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Template_CompanyIntroduction",
                table: "InterviewTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InterviewSessions_Status_NextRecoveryAtUtc_ProcessingLeaseE~",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_Interactions",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_Recovery",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_Stage",
                table: "InterviewSessions");

            migrationBuilder.DropIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_QuestionId",
                table: "InterviewAnswers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Answer_Interaction",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "CandidateIntroductionEnabled",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CandidateIntroductionMessage",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CompanyIntroductionEnabled",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CompanyIntroductionMessage",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "CandidateIntroductionEnabled",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "CandidateIntroductionMessage",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "CandidateIntroductionTranscript",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "CompanyIntroductionEnabled",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "CompanyIntroductionMessage",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "CurrentInteractionMessage",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "MasterPromptSnapshot",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "MasterPromptVersion",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "MaxInteractionsPerQuestion",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "NextRecoveryAtUtc",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "RecoveryAttempts",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "ResumeTextSnapshot",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "StageChangedAtUtc",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "AssistantMessage",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "IsInteraction",
                table: "InterviewAnswers");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_QuestionId",
                table: "InterviewAnswers",
                columns: new[] { "InterviewSessionId", "QuestionId" },
                unique: true);
        }
    }
}
