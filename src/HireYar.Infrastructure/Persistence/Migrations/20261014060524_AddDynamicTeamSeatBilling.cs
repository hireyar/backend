using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicTeamSeatBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Purchase_Operation",
                table: "SubscriptionPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payment_Values",
                table: "Payments");

            migrationBuilder.AddColumn<int>(
                name: "IncludedTeamUsersSnapshot",
                table: "SubscriptionPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaidTeamUserLimitSnapshot",
                table: "SubscriptionPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamCapacitySnapshot",
                table: "SubscriptionPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TeamSeatAmountIrtSnapshot",
                table: "SubscriptionPurchases",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TeamUserMonthlyPriceIrtSnapshot",
                table: "SubscriptionPurchases",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "IncludedTeamUsers",
                table: "RegistrationTrials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaidTeamUserLimit",
                table: "RegistrationTrials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TeamUserMonthlyPriceIrt",
                table: "RegistrationTrials",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "IncludedTeamUsers",
                table: "PricingPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaidTeamUserLimit",
                table: "PricingPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TeamUserMonthlyPriceIrt",
                table: "PricingPackages",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TeamSeatPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousCapacity = table.Column<int>(type: "integer", nullable: false),
                    TargetCapacity = table.Column<int>(type: "integer", nullable: false),
                    MonthlyPriceIrt = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    AmountIrt = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QuotedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamSeatPurchases", x => x.Id);
                    table.UniqueConstraint("AK_TeamSeatPurchases_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_TeamSeat_Values", "\"PreviousCapacity\" >= 1 AND \"TargetCapacity\" BETWEEN \"PreviousCapacity\" + 1 AND 10000 AND \"MonthlyPriceIrt\" > 0 AND \"AmountIrt\" > 0 AND \"ExpiresAtUtc\" > \"QuotedAtUtc\" AND \"CreatedAtUtc\" >= \"QuotedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_TeamSeatPurchases_OrganizationSubscriptions_OrganizationId_~",
                        columns: x => new { x.OrganizationId, x.SubscriptionId },
                        principalTable: "OrganizationSubscriptions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamSeatPurchases_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamSeatPurchases_Payments_OrganizationId_PaymentId",
                        columns: x => new { x.OrganizationId, x.PaymentId },
                        principalTable: "Payments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE "PricingPackages" SET "IncludedTeamUsers" = CASE WHEN "Code" = 'advanced' THEN 5 ELSE 3 END,
                  "PaidTeamUserLimit" = 30, "TeamUserMonthlyPriceIrt" = 200000;
                ALTER TABLE "SubscriptionPurchases" DISABLE TRIGGER purchase_immutable;
                UPDATE "SubscriptionPurchases" p SET
                  "IncludedTeamUsersSnapshot" = CASE WHEN "PackageCodeSnapshot" = 'advanced' THEN 5 ELSE 3 END,
                  "PaidTeamUserLimitSnapshot" = 30, "TeamUserMonthlyPriceIrtSnapshot" = 200000,
                  "TeamCapacitySnapshot" = LEAST(30, GREATEST(CASE WHEN "PackageCodeSnapshot" = 'advanced' THEN 5 ELSE 3 END,
                    (SELECT COUNT(*) FROM "AspNetUsers" u WHERE u."OrganizationId" = p."OrganizationId" AND u."IsActive") +
                    (SELECT COUNT(*) FROM "MobileTeamInvitations" i WHERE i."OrganizationId" = p."OrganizationId"
                      AND i."AcceptedAtUtc" IS NULL AND i."CancelledAtUtc" IS NULL AND i."ExpiresAtUtc" > now())));
                ALTER TABLE "SubscriptionPurchases" ENABLE TRIGGER purchase_immutable;
                ALTER TABLE "RegistrationTrials" DISABLE TRIGGER USER;
                UPDATE "RegistrationTrials" t SET "IncludedTeamUsers" = LEAST(30, GREATEST(5,
                    (SELECT COUNT(*) FROM "AspNetUsers" u WHERE u."OrganizationId" = t."OrganizationId" AND u."IsActive") +
                    (SELECT COUNT(*) FROM "MobileTeamInvitations" i WHERE i."OrganizationId" = t."OrganizationId"
                      AND i."AcceptedAtUtc" IS NULL AND i."CancelledAtUtc" IS NULL AND i."ExpiresAtUtc" > now()))),
                  "PaidTeamUserLimit" = 30, "TeamUserMonthlyPriceIrt" = 200000;
                ALTER TABLE "RegistrationTrials" ENABLE TRIGGER USER;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Purchase_Operation",
                table: "SubscriptionPurchases",
                sql: "\"Operation\" IN ('purchase','renewal','upgrade') AND \"ProratedCreditAmountSnapshot\" BETWEEN 0 AND (\"PriceAmountSnapshot\" + \"TeamSeatAmountIrtSnapshot\") AND ((\"Operation\" = 'upgrade' AND \"ReplacesSubscriptionId\" IS NOT NULL) OR (\"Operation\" <> 'upgrade' AND \"ReplacesSubscriptionId\" IS NULL AND \"ProratedCreditAmountSnapshot\" = 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Purchase_Team",
                table: "SubscriptionPurchases",
                sql: "\"IncludedTeamUsersSnapshot\" >= 1 AND \"PaidTeamUserLimitSnapshot\" BETWEEN \"IncludedTeamUsersSnapshot\" AND 10000 AND \"TeamUserMonthlyPriceIrtSnapshot\" BETWEEN 0 AND 1000000000000 AND \"TeamCapacitySnapshot\" BETWEEN \"IncludedTeamUsersSnapshot\" AND \"PaidTeamUserLimitSnapshot\" AND \"TeamSeatAmountIrtSnapshot\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trial_Team",
                table: "RegistrationTrials",
                sql: "\"IncludedTeamUsers\" >= 1 AND \"PaidTeamUserLimit\" BETWEEN \"IncludedTeamUsers\" AND 10000 AND \"TeamUserMonthlyPriceIrt\" BETWEEN 0 AND 1000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Package_Team",
                table: "PricingPackages",
                sql: "\"IncludedTeamUsers\" >= 1 AND \"PaidTeamUserLimit\" BETWEEN \"IncludedTeamUsers\" AND 10000 AND \"TeamUserMonthlyPriceIrt\" BETWEEN 0 AND 1000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payment_Values",
                table: "Payments",
                sql: "\"Amount\" > 0 AND \"Currency\" = 'IRT' AND \"Status\" IN ('Pending','Paid','Failed','Cancelled','Expired') AND \"Type\" IN ('Subscription','AiCredit','TeamSeats') AND (\"Type\" <> 'AiCredit' OR (\"CreditPackageId\" IS NOT NULL AND \"CreditAmountUsdSnapshot\" > 0 AND \"EffectiveUsdToIrtSnapshot\" > 0)) AND (\"Status\" <> 'Paid' OR (\"PaidAtUtc\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeatPurchases_OrganizationId_PaymentId",
                table: "TeamSeatPurchases",
                columns: new[] { "OrganizationId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeatPurchases_OrganizationId_SubscriptionId",
                table: "TeamSeatPurchases",
                columns: new[] { "OrganizationId", "SubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamSeatPurchases_PaymentId",
                table: "TeamSeatPurchases",
                column: "PaymentId",
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER team_seat_immutable BEFORE UPDATE OR DELETE ON "TeamSeatPurchases"
                  FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();
                CREATE FUNCTION hireyar_team_seat_contract() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE payment_id uuid; org_id uuid; seat "TeamSeatPurchases"%ROWTYPE;
                  pay "Payments"%ROWTYPE; sub "OrganizationSubscriptions"%ROWTYPE;
                  monthly numeric; paid_limit integer; base_capacity integer;
                BEGIN
                  IF TG_TABLE_NAME = 'Payments' THEN
                    IF NEW."Type" <> 'TeamSeats' THEN RETURN NULL; END IF;
                    payment_id := NEW."Id"; org_id := NEW."OrganizationId";
                  ELSE payment_id := NEW."PaymentId"; org_id := NEW."OrganizationId"; END IF;
                  SELECT * INTO seat FROM "TeamSeatPurchases" WHERE "PaymentId" = payment_id AND "OrganizationId" = org_id;
                  SELECT * INTO pay FROM "Payments" WHERE "Id" = payment_id AND "OrganizationId" = org_id;
                  SELECT * INTO sub FROM "OrganizationSubscriptions" WHERE "Id" = seat."SubscriptionId" AND "OrganizationId" = org_id;
                  IF sub."IsTrial" THEN
                    SELECT "TeamUserMonthlyPriceIrt", "PaidTeamUserLimit", "IncludedTeamUsers" INTO monthly, paid_limit, base_capacity
                      FROM "RegistrationTrials" WHERE "Id" = sub."Id" AND "OrganizationId" = org_id;
                  ELSE
                    SELECT "TeamUserMonthlyPriceIrtSnapshot", "PaidTeamUserLimitSnapshot", "TeamCapacitySnapshot" INTO monthly, paid_limit, base_capacity
                      FROM "SubscriptionPurchases" WHERE "PaymentId" = sub."PaymentId" AND "OrganizationId" = org_id;
                  END IF;
                  IF seat."Id" IS NULL OR pay."Id" IS NULL OR sub."Id" IS NULL OR monthly IS NULL OR
                    pay."Type" <> 'TeamSeats' OR pay."Amount" <> seat."AmountIrt" OR
                    seat."MonthlyPriceIrt" <> monthly OR seat."PreviousCapacity" < base_capacity OR
                    seat."TargetCapacity" > paid_limit OR seat."ExpiresAtUtc" <> sub."ExpiresAtUtc" OR
                    seat."QuotedAtUtc" < sub."StartedAtUtc" OR
                    seat."AmountIrt" <> CEIL((seat."TargetCapacity" - seat."PreviousCapacity") * monthly *
                      EXTRACT(EPOCH FROM (seat."ExpiresAtUtc" - seat."QuotedAtUtc")) / 2592000) THEN
                    RAISE EXCEPTION 'Team seat contract mismatch' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER team_seat_contract AFTER INSERT ON "TeamSeatPurchases"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hireyar_team_seat_contract();
                CREATE CONSTRAINT TRIGGER team_seat_payment_contract AFTER INSERT OR UPDATE ON "Payments"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hireyar_team_seat_contract();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "RegistrationTrials") THEN
                    RAISE EXCEPTION 'Cannot remove registration trial history' USING ERRCODE = '23514';
                  END IF;
                  IF EXISTS (SELECT 1 FROM "TeamSeatPurchases") OR
                     EXISTS (SELECT 1 FROM "SubscriptionPurchases" WHERE "TeamSeatAmountIrtSnapshot" <> 0 OR "TeamCapacitySnapshot" > "IncludedTeamUsersSnapshot") OR
                     EXISTS (SELECT 1 FROM "PricingPackages" WHERE
                       "IncludedTeamUsers" <> CASE WHEN "Code" = 'advanced' THEN 5 ELSE 3 END OR
                       "PaidTeamUserLimit" <> 30 OR "TeamUserMonthlyPriceIrt" <> 200000) THEN
                    RAISE EXCEPTION 'Cannot discard team billing contracts or administrator settings';
                  END IF;
                END $$;
                DROP FUNCTION hireyar_team_seat_contract() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "TeamSeatPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Purchase_Operation",
                table: "SubscriptionPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Purchase_Team",
                table: "SubscriptionPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trial_Team",
                table: "RegistrationTrials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Package_Team",
                table: "PricingPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payment_Values",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IncludedTeamUsersSnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "PaidTeamUserLimitSnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "TeamCapacitySnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "TeamSeatAmountIrtSnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "TeamUserMonthlyPriceIrtSnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "IncludedTeamUsers",
                table: "RegistrationTrials");

            migrationBuilder.DropColumn(
                name: "PaidTeamUserLimit",
                table: "RegistrationTrials");

            migrationBuilder.DropColumn(
                name: "TeamUserMonthlyPriceIrt",
                table: "RegistrationTrials");

            migrationBuilder.DropColumn(
                name: "IncludedTeamUsers",
                table: "PricingPackages");

            migrationBuilder.DropColumn(
                name: "PaidTeamUserLimit",
                table: "PricingPackages");

            migrationBuilder.DropColumn(
                name: "TeamUserMonthlyPriceIrt",
                table: "PricingPackages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Purchase_Operation",
                table: "SubscriptionPurchases",
                sql: "\"Operation\" IN ('purchase','renewal','upgrade') AND \"ProratedCreditAmountSnapshot\" BETWEEN 0 AND \"PriceAmountSnapshot\" AND ((\"Operation\" = 'upgrade' AND \"ReplacesSubscriptionId\" IS NOT NULL) OR (\"Operation\" <> 'upgrade' AND \"ReplacesSubscriptionId\" IS NULL AND \"ProratedCreditAmountSnapshot\" = 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payment_Values",
                table: "Payments",
                sql: "\"Amount\" > 0 AND \"Currency\" = 'IRT' AND \"Status\" IN ('Pending','Paid','Failed','Cancelled','Expired') AND \"Type\" IN ('Subscription','AiCredit') AND (\"Type\" <> 'AiCredit' OR (\"CreditPackageId\" IS NOT NULL AND \"CreditAmountUsdSnapshot\" > 0 AND \"EffectiveUsdToIrtSnapshot\" > 0)) AND (\"Status\" <> 'Paid' OR (\"PaidAtUtc\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL))");
        }
    }
}
