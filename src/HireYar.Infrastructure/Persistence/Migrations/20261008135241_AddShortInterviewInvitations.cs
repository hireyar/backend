using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShortInterviewInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessFailedAttempts",
                table: "InterviewSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AccessFailureWindowAtUtc",
                table: "InterviewSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AccessLockedUntilUtc",
                table: "InterviewSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedAccessCode",
                table: "InterviewSessions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByInterviewId",
                table: "InterviewSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_SupersededByInterviewId",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "SupersededByInterviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_SupersededByInterviewId",
                table: "InterviewSessions",
                column: "SupersededByInterviewId",
                unique: true,
                filter: "\"SupersededByInterviewId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_AccessAttempts",
                table: "InterviewSessions",
                sql: "\"AccessFailedAttempts\" BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_Replacement",
                table: "InterviewSessions",
                sql: "\"SupersededByInterviewId\" IS NULL OR (\"SupersededByInterviewId\" <> \"Id\" AND \"AccessRevokedAtUtc\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewSessions_InterviewSessions_OrganizationId_Supersed~",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "SupersededByInterviewId" },
                principalTable: "InterviewSessions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "InterviewSessions" WHERE "ProtectedAccessCode" IS NOT NULL OR "SupersededByInterviewId" IS NOT NULL OR "AccessFailedAttempts" > 0) THEN
                    RAISE EXCEPTION 'Cannot downgrade while short invitation codes, replacement history or access failures exist.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_InterviewSessions_InterviewSessions_OrganizationId_Supersed~",
                table: "InterviewSessions");

            migrationBuilder.DropIndex(
                name: "IX_InterviewSessions_OrganizationId_SupersededByInterviewId",
                table: "InterviewSessions");

            migrationBuilder.DropIndex(
                name: "IX_InterviewSessions_SupersededByInterviewId",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_AccessAttempts",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_Replacement",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "AccessFailedAttempts",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "AccessFailureWindowAtUtc",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "AccessLockedUntilUtc",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "ProtectedAccessCode",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "SupersededByInterviewId",
                table: "InterviewSessions");
        }
    }
}
