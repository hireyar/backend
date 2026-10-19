using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSignupTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignupTrials",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InterviewQuantity = table.Column<int>(type: "integer", nullable: false),
                    TeamUsers = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupTrials", x => x.OrganizationId);
                    table.CheckConstraint("CK_SignupTrial_Values", "\"ExpiresAtUtc\" = \"StartedAtUtc\" + interval '48 hours' AND \"InterviewQuantity\" = 2 AND \"TeamUsers\" >= 1 AND \"RetentionDays\" BETWEEN 1 AND 3650");
                    table.ForeignKey(
                        name: "FK_SignupTrials_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.Sql("""
                CREATE TRIGGER signup_trial_immutable BEFORE UPDATE OR DELETE ON "SignupTrials"
                    FOR EACH ROW EXECUTE FUNCTION hy_commerce_immutable();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SignupTrials") THEN
                        RAISE EXCEPTION 'Restore a backup before discarding registration trial history';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "SignupTrials");
        }
    }
}
