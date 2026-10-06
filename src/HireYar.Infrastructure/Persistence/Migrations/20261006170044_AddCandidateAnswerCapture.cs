using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateAnswerCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AskedAtUtc",
                table: "InterviewQuestionSnapshots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientTurnId",
                table: "InterviewAnswers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "InterviewAnswers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSpeechEndUtc",
                table: "InterviewAnswers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoContentType",
                table: "InterviewAnswers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VideoSizeBytes",
                table: "InterviewAnswers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoStorageKey",
                table: "InterviewAnswers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_ClientTurnId",
                table: "InterviewAnswers",
                columns: new[] { "InterviewSessionId", "ClientTurnId" },
                unique: true,
                filter: "\"ClientTurnId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Answer_Video",
                table: "InterviewAnswers",
                sql: "(\"VideoStorageKey\" IS NULL AND \"VideoContentType\" IS NULL AND \"VideoSizeBytes\" IS NULL) OR (\"VideoStorageKey\" IS NOT NULL AND \"VideoContentType\" IS NOT NULL AND \"VideoSizeBytes\" IS NOT NULL AND \"VideoSizeBytes\" > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_ClientTurnId",
                table: "InterviewAnswers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Answer_Video",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "AskedAtUtc",
                table: "InterviewQuestionSnapshots");

            migrationBuilder.DropColumn(
                name: "ClientTurnId",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "LastSpeechEndUtc",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "VideoContentType",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "VideoSizeBytes",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "VideoStorageKey",
                table: "InterviewAnswers");
        }
    }
}
