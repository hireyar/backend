using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentId",
                table: "OrganizationSubscriptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                table: "OrganizationSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RegistrationTrials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreditAmountUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    AiPricePerMinuteUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationTrials", x => x.Id);
                    table.UniqueConstraint("AK_RegistrationTrials_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_RegistrationTrial_Values", "\"CreditAmountUsd\" = 1 AND \"AiPricePerMinuteUsd\" > 0 AND \"RetentionDays\" BETWEEN 1 AND 3650");
                    table.ForeignKey(
                        name: "FK_RegistrationTrials_OrganizationSubscriptions_OrganizationId~",
                        columns: x => new { x.OrganizationId, x.Id },
                        principalTable: "OrganizationSubscriptions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrationTrials_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationTrialEntitlement",
                columns: table => new
                {
                    TrialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationTrialEntitlement", x => new { x.TrialId, x.Code });
                    table.ForeignKey(
                        name: "FK_RegistrationTrialEntitlement_RegistrationTrials_TrialId",
                        column: x => x.TrialId,
                        principalTable: "RegistrationTrials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_OrganizationId",
                table: "OrganizationSubscriptions",
                column: "OrganizationId",
                unique: true,
                filter: "\"IsTrial\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_Source",
                table: "OrganizationSubscriptions",
                sql: "(\"IsTrial\" AND \"PaymentId\" IS NULL AND \"ExpiresAtUtc\" = \"StartedAtUtc\" + interval '72 hours') OR (NOT \"IsTrial\" AND \"PaymentId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationTrials_OrganizationId",
                table: "RegistrationTrials",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationTrials_OrganizationId_CreatedAtUtc_Id",
                table: "RegistrationTrials",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.Sql("""
                CREATE TRIGGER registration_trial_immutable BEFORE UPDATE OR DELETE ON "RegistrationTrials"
                FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();
                CREATE TRIGGER registration_trial_entitlement_immutable BEFORE UPDATE OR DELETE ON "RegistrationTrialEntitlement"
                FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();
                CREATE FUNCTION hireyar_registration_trial_complete() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW."IsTrial" AND NOT EXISTS (
                    SELECT 1 FROM "RegistrationTrials" g JOIN "AiCreditTransactions" c
                      ON c."OrganizationId" = g."OrganizationId" AND c."ReferenceId" = g."Id"
                    WHERE g."Id" = NEW."Id" AND g."OrganizationId" = NEW."OrganizationId"
                      AND g."CreatedAtUtc" = NEW."StartedAtUtc" AND c."ReferenceType" = 'RegistrationTrial'
                      AND c."Type" = 'PromotionalCredit' AND c."Amount" = 1
                      AND c."BalanceBefore" = 0 AND c."BalanceAfter" = 1
                      AND EXISTS (SELECT 1 FROM "RegistrationTrialEntitlement" e WHERE e."TrialId" = g."Id")
                  ) THEN RAISE EXCEPTION 'Incomplete registration trial' USING ERRCODE = '23514'; END IF;
                  RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER registration_trial_complete AFTER INSERT ON "OrganizationSubscriptions"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hireyar_registration_trial_complete();
                CREATE FUNCTION hireyar_registration_trial_source() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM "OrganizationSubscriptions" s
                    WHERE s."Id" = NEW."Id" AND s."OrganizationId" = NEW."OrganizationId" AND s."IsTrial")
                  THEN RAISE EXCEPTION 'Invalid registration trial source' USING ERRCODE = '23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER registration_trial_source BEFORE INSERT ON "RegistrationTrials"
                FOR EACH ROW EXECUTE FUNCTION hireyar_registration_trial_source();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "OrganizationSubscriptions" WHERE "IsTrial")
                  THEN RAISE EXCEPTION 'Cannot remove registration trial history' USING ERRCODE = '23514'; END IF;
                END $$;
                DROP FUNCTION hireyar_registration_trial_complete() CASCADE;
                DROP FUNCTION hireyar_registration_trial_source() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "RegistrationTrialEntitlement");

            migrationBuilder.DropTable(
                name: "RegistrationTrials");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationSubscriptions_OrganizationId",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_Source",
                table: "OrganizationSubscriptions");

            migrationBuilder.DropColumn(
                name: "IsTrial",
                table: "OrganizationSubscriptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentId",
                table: "OrganizationSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
