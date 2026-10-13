using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileTeamInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_ActiveOwner",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "MobileTeamInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mobile = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileTeamInvitations", x => x.Id);
                    table.CheckConstraint("CK_MobileTeamInvitation_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_MobileTeamInvitation_Mobile", "\"Mobile\" ~ '^09[0-9]{9}$'");
                    table.CheckConstraint("CK_MobileTeamInvitation_State", "\"AcceptedAtUtc\" IS NULL OR \"CancelledAtUtc\" IS NULL");
                    table.ForeignKey(
                        name: "FK_MobileTeamInvitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId",
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"Role\" = 'Owner'");

            migrationBuilder.CreateIndex(
                name: "IX_MobileTeamInvitations_Mobile",
                table: "MobileTeamInvitations",
                column: "Mobile",
                unique: true,
                filter: "\"AcceptedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MobileTeamInvitations_OrganizationId_CreatedAtUtc",
                table: "MobileTeamInvitations",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "MobileTeamInvitations") OR
                       EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "IsActive" AND "Role" <> 'Owner') THEN
                        RAISE EXCEPTION 'Cannot downgrade while mobile team membership or invitation history exists.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "MobileTeamInvitations");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers");

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
    }
}
