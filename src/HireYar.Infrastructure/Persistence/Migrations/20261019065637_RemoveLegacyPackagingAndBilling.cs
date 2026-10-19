using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyPackagingAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InterviewSessions_InterviewTypes_PricingInterviewTypeCode",
                table: "InterviewSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_InterviewTemplates_InterviewTypes_InterviewType",
                table: "InterviewTemplates");

            // Catalog codes may be custom; preserve their execution strategy before dropping the catalog.
            migrationBuilder.Sql("""
                UPDATE "InterviewTemplates" AS template
                SET "InterviewType" = types."EngineType"
                FROM "InterviewTypes" AS types WHERE types."Code" = template."InterviewType";
                """);

            migrationBuilder.DropTable(
                name: "AiCreditReservations");

            migrationBuilder.DropTable(
                name: "AiCreditTransactions");

            migrationBuilder.DropTable(
                name: "InterviewBillings");

            migrationBuilder.DropTable(
                name: "PackageFeatures");

            migrationBuilder.DropTable(
                name: "PackageInterviewTypes");

            migrationBuilder.DropTable(
                name: "RegistrationTrialEntitlement");

            migrationBuilder.DropTable(
                name: "SubscriptionEntitlement");

            migrationBuilder.DropTable(
                name: "SubscriptionReplacements");

            migrationBuilder.DropTable(
                name: "TeamSeatPurchases");

            migrationBuilder.DropTable(
                name: "OrganizationAiWallets");

            migrationBuilder.DropTable(
                name: "FeatureDefinitions");

            migrationBuilder.DropTable(
                name: "InterviewTypes");

            migrationBuilder.DropTable(
                name: "RegistrationTrials");

            migrationBuilder.DropTable(
                name: "SubscriptionPurchases");

            migrationBuilder.DropTable(
                name: "OrganizationSubscriptions");

            migrationBuilder.DropTable(
                name: "PackagePeriods");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PricingPackages");

            migrationBuilder.DropTable(
                name: "AiCreditPackages");

            migrationBuilder.DropIndex(
                name: "IX_InterviewTemplates_InterviewType",
                table: "InterviewTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InterviewSessions_PricingInterviewTypeCode",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_Capability",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "PricingInterviewTypeCode",
                table: "InterviewSessions");

            migrationBuilder.RenameColumn(
                name: "RequiredCapabilityCode",
                table: "InterviewSessions",
                newName: "InterviewType");

            migrationBuilder.Sql("""
                UPDATE "InterviewSessions" SET "InterviewType" =
                    CASE WHEN "InterviewType" = 'hr_interview' THEN 'general' ELSE 'specialized' END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_Type",
                table: "InterviewSessions",
                sql: "\"InterviewType\" IN ('general','specialized')");

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS hireyar_billing_immutable();
                DROP FUNCTION IF EXISTS hireyar_subscription_no_overlap();
                DROP FUNCTION IF EXISTS hireyar_payment_guard();
                DROP FUNCTION IF EXISTS hireyar_interview_billing_guard();
                DROP FUNCTION IF EXISTS hireyar_reservation_guard();
                DROP FUNCTION IF EXISTS hireyar_wallet_matches_ledger();
                DROP FUNCTION IF EXISTS hireyar_replacement_guard();
                DROP FUNCTION IF EXISTS hireyar_replacement_fulfilled();
                DROP FUNCTION IF EXISTS hireyar_registration_trial_complete();
                DROP FUNCTION IF EXISTS hireyar_registration_trial_source();
                DROP FUNCTION IF EXISTS hireyar_team_seat_contract();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    RAISE EXCEPTION 'Legacy packaging and financial records were explicitly deleted. Restore a pre-removal backup to roll back.';
                END $$;
                """);
        }
    }
}
