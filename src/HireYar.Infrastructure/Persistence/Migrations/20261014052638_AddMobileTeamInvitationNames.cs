using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileTeamInvitationNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "MobileTeamInvitations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "MobileTeamInvitations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MobileTeamInvitation_Name",
                table: "MobileTeamInvitations",
                sql: "(\"FirstName\" IS NULL AND \"LastName\" IS NULL) OR (\"FirstName\" IS NOT NULL AND \"LastName\" IS NOT NULL AND length(\"FirstName\") > 0 AND length(\"LastName\") > 0 AND length(\"FirstName\") + 1 + length(\"LastName\") <= 200)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "MobileTeamInvitations" WHERE "FirstName" IS NOT NULL OR "LastName" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade while invitation names exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_MobileTeamInvitation_Name",
                table: "MobileTeamInvitations");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "MobileTeamInvitations");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "MobileTeamInvitations");
        }
    }
}
