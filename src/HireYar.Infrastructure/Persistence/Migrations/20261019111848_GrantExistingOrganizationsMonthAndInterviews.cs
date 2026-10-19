using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantExistingOrganizationsMonthAndInterviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExistingOrganizationGrants",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InterviewQuantity = table.Column<int>(type: "integer", nullable: false),
                    TeamUsers = table.Column<int>(type: "integer", nullable: false),
                    UnlimitedTeam = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExistingOrganizationGrants", x => x.OrganizationId);
                    table.CheckConstraint("CK_ExistingOrganizationGrant_Values", "\"ExpiresAtUtc\" = ((\"StartedAtUtc\" AT TIME ZONE 'UTC') + interval '1 month') AT TIME ZONE 'UTC' AND \"InterviewQuantity\" = 100 AND \"TeamUsers\" >= 1 AND \"RetentionDays\" BETWEEN 1 AND 3650");
                    table.ForeignKey(
                        name: "FK_ExistingOrganizationGrants_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.Sql("""
                LOCK TABLE "Organizations", "OrganizationMemberships", "SignupTrials" IN SHARE ROW EXCLUSIVE MODE;
                INSERT INTO "ExistingOrganizationGrants"
                    ("OrganizationId", "CreatedAtUtc", "StartedAtUtc", "ExpiresAtUtc", "InterviewQuantity", "TeamUsers", "UnlimitedTeam", "RetentionDays")
                SELECT o."Id", CURRENT_TIMESTAMP, v.starts_at,
                    ((v.starts_at AT TIME ZONE 'UTC') + interval '1 month') AT TIME ZONE 'UTC',
                    100, v.team_users, COALESCE(m."UnlimitedTeam", false) OR v.team_users >= p."PaidUserCeiling",
                    COALESCE(m."RetentionDays", t."RetentionDays", p."RetentionDays")
                FROM "Organizations" o CROSS JOIN "SubscriptionPlans" p
                LEFT JOIN LATERAL (
                    SELECT * FROM "OrganizationMemberships" m WHERE m."OrganizationId" = o."Id" AND m."ExpiresAtUtc" > CURRENT_TIMESTAMP
                    ORDER BY m."ExpiresAtUtc" DESC, m."Id" LIMIT 1
                ) m ON true
                LEFT JOIN "SignupTrials" t ON t."OrganizationId" = o."Id"
                CROSS JOIN LATERAL (
                    SELECT GREATEST(CURRENT_TIMESTAMP, m."ExpiresAtUtc", t."ExpiresAtUtc") AS starts_at,
                        GREATEST(p."IncludedUsers", COALESCE(m."TeamUsers", t."TeamUsers", 0),
                            ((SELECT count(*) FROM "AspNetUsers" u WHERE u."OrganizationId" = o."Id" AND u."IsActive") +
                             (SELECT count(*) FROM "MobileTeamInvitations" i WHERE i."OrganizationId" = o."Id" AND
                                i."AcceptedAtUtc" IS NULL AND i."CancelledAtUtc" IS NULL AND i."ExpiresAtUtc" > CURRENT_TIMESTAMP))::integer) AS team_users
                ) v;
                CREATE TRIGGER existing_organization_grant_immutable BEFORE UPDATE OR DELETE ON "ExistingOrganizationGrants"
                    FOR EACH ROW EXECUTE FUNCTION hy_commerce_immutable();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "ExistingOrganizationGrants") THEN
                        RAISE EXCEPTION 'Restore a backup before discarding existing organization gifts';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "ExistingOrganizationGrants");
        }
    }
}
