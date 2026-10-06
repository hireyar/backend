using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAccountsAndPanelDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve historical identities and invitations, but retire all team access before enforcing one active Owner.
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" SET "IsActive" = FALSE WHERE "Role" <> 'Owner' AND "IsActive";
                UPDATE "RefreshTokens" SET "RevokedAtUtc" = CURRENT_TIMESTAMP
                WHERE "RevokedAtUtc" IS NULL AND "UserId" IN (SELECT "Id" FROM "AspNetUsers" WHERE "Role" <> 'Owner');
                UPDATE "TeamInvitations" SET "CancelledAtUtc" = CURRENT_TIMESTAMP
                WHERE "AcceptedAtUtc" IS NULL AND "CancelledAtUtc" IS NULL;
                """);
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "JobPositions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "InterviewTemplates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "InterviewSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_ActiveOwner",
                table: "AspNetUsers",
                sql: "NOT \"IsActive\" OR \"Role\" = 'Owner'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_ActiveOwner",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "JobPositions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "InterviewTemplates");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "InterviewSessions");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId",
                unique: true,
                filter: "\"Role\" = 'Owner'");
        }
    }
}
