using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureRegistrationTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TrialCompletionSql(configurable: true));
            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistrationTrial_Values",
                table: "RegistrationTrials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_Source",
                table: "OrganizationSubscriptions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistrationTrial_Values",
                table: "RegistrationTrials",
                sql: "\"CreditAmountUsd\" > 0 AND \"AiPricePerMinuteUsd\" > 0 AND \"RetentionDays\" BETWEEN 1 AND 3650");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_Source",
                table: "OrganizationSubscriptions",
                sql: "(\"IsTrial\" AND \"PaymentId\" IS NULL AND EXTRACT(EPOCH FROM (\"ExpiresAtUtc\" - \"StartedAtUtc\")) BETWEEN 86400 AND 315360000 AND MOD(EXTRACT(EPOCH FROM (\"ExpiresAtUtc\" - \"StartedAtUtc\")), 86400) = 0) OR (NOT \"IsTrial\" AND \"PaymentId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "RegistrationTrials" WHERE "CreditAmountUsd" <> 1)
                    OR EXISTS (SELECT 1 FROM "OrganizationSubscriptions" WHERE "IsTrial"
                      AND "ExpiresAtUtc" <> "StartedAtUtc" + interval '72 hours')
                  THEN RAISE EXCEPTION 'Cannot downgrade configured registration trial history' USING ERRCODE = '23514'; END IF;
                END $$;
                """);
            migrationBuilder.Sql(TrialCompletionSql(configurable: false));
            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistrationTrial_Values",
                table: "RegistrationTrials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscription_Source",
                table: "OrganizationSubscriptions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistrationTrial_Values",
                table: "RegistrationTrials",
                sql: "\"CreditAmountUsd\" = 1 AND \"AiPricePerMinuteUsd\" > 0 AND \"RetentionDays\" BETWEEN 1 AND 3650");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscription_Source",
                table: "OrganizationSubscriptions",
                sql: "(\"IsTrial\" AND \"PaymentId\" IS NULL AND \"ExpiresAtUtc\" = \"StartedAtUtc\" + interval '72 hours') OR (NOT \"IsTrial\" AND \"PaymentId\" IS NOT NULL)");
        }

        private static string TrialCompletionSql(bool configurable) => $$"""
            CREATE OR REPLACE FUNCTION hireyar_registration_trial_complete() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NEW."IsTrial" AND NOT EXISTS (
                SELECT 1 FROM "RegistrationTrials" g JOIN "AiCreditTransactions" c
                  ON c."OrganizationId" = g."OrganizationId" AND c."ReferenceId" = g."Id"
                WHERE g."Id" = NEW."Id" AND g."OrganizationId" = NEW."OrganizationId"
                  AND g."CreatedAtUtc" = NEW."StartedAtUtc" AND c."ReferenceType" = 'RegistrationTrial'
                  AND c."Type" = 'PromotionalCredit' AND c."Amount" = {{(configurable ? "g.\"CreditAmountUsd\"" : "1")}}
                  AND c."BalanceBefore" = 0 AND c."BalanceAfter" = {{(configurable ? "g.\"CreditAmountUsd\"" : "1")}}
                  AND EXISTS (SELECT 1 FROM "RegistrationTrialEntitlement" e WHERE e."TrialId" = g."Id")
              ) THEN RAISE EXCEPTION 'Incomplete registration trial' USING ERRCODE = '23514'; END IF;
              RETURN NULL;
            END $$;
            """;
    }
}
