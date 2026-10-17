using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;

#nullable disable
namespace HireYar.Infrastructure.Persistence.Migrations;

public partial class PersistRialAiCredit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Offline cutover only: explicit, integer IRR/USD prevents rounding drift in legacy ledger chains.
        // PostgreSQL rolls back DDL, balances and history together if any check fails.
        migrationBuilder.Sql("""
            LOCK TABLE "OrganizationAiWallets", "AiCreditTransactions", "AiCreditReservations", "InterviewBillings",
                "Payments", "RegistrationTrials", "AiCreditPackages" IN ACCESS EXCLUSIVE MODE;
            DO $$
            DECLARE rate numeric := NULLIF(current_setting('hireyar.ai_credit_cutover_rial_per_usd', true), '')::numeric;
            BEGIN
                IF EXISTS (SELECT 1 FROM "OrganizationAiWallets") OR EXISTS (SELECT 1 FROM "AiCreditPackages")
                   OR EXISTS (SELECT 1 FROM "RegistrationTrials") OR EXISTS (SELECT 1 FROM "InterviewBillings")
                   OR EXISTS (SELECT 1 FROM "Payments" WHERE "CreditAmountUsdSnapshot" IS NOT NULL) THEN
                    IF rate IS NULL OR rate <= 0 OR rate > 10000000000 OR rate <> trunc(rate) THEN
                        RAISE EXCEPTION 'Set hireyar.ai_credit_cutover_rial_per_usd to an explicit positive integer IRR/USD rate before migrating existing AI credit.';
                    END IF;
                ELSE rate := 1; END IF;
                PERFORM set_config('hireyar.ai_credit_cutover_rial_per_usd', rate::text, true);
            END $$;
            CREATE TABLE "AiCreditCurrencyCutover" ("Id" integer PRIMARY KEY CHECK ("Id" = 1),
                "RialPerUsd" numeric(24,6) NOT NULL, "ConvertedAtUtc" timestamptz NOT NULL);
            INSERT INTO "AiCreditCurrencyCutover" VALUES (1, current_setting('hireyar.ai_credit_cutover_rial_per_usd')::numeric, now());

            ALTER TABLE "OrganizationAiWallets" DISABLE TRIGGER USER;
            ALTER TABLE "AiCreditTransactions" DISABLE TRIGGER USER;
            ALTER TABLE "AiCreditReservations" DISABLE TRIGGER USER;
            ALTER TABLE "InterviewBillings" DISABLE TRIGGER USER;
            ALTER TABLE "Payments" DISABLE TRIGGER USER;
            ALTER TABLE "RegistrationTrials" DISABLE TRIGGER USER;
            ALTER TABLE "AiCreditPackages" DISABLE TRIGGER USER;

            ALTER TABLE "OrganizationAiWallets" DROP CONSTRAINT "CK_Wallet_Values";
            ALTER TABLE "InterviewBillings" DROP CONSTRAINT "CK_InterviewBilling_Values";
            ALTER TABLE "RegistrationTrials" RENAME COLUMN "CreditAmountUsd" TO "CreditAmountRial";
            ALTER TABLE "Payments" RENAME COLUMN "CreditAmountUsdSnapshot" TO "CreditAmountRialSnapshot";
            ALTER TABLE "Payments" RENAME COLUMN "EffectiveUsdToIrtSnapshot" TO "EffectiveCreditRialToIrtSnapshot";
            ALTER TABLE "AiCreditPackages" RENAME COLUMN "CreditAmountUsd" TO "CreditAmountRial";
            ALTER TABLE "AiCreditReservations" RENAME COLUMN "ReservedAmountUsd" TO "ReservedAmountRial";
            ALTER TABLE "AiCreditReservations" RENAME COLUMN "ConsumedAmountUsd" TO "ConsumedAmountRial";
            ALTER TABLE "InterviewBillings" ADD COLUMN "BilledAmountRial" numeric(24,6) NOT NULL DEFAULT 0;
            ALTER TABLE "AiCreditTransactions" ADD COLUMN "AmountUsd" numeric(18,6);

            ALTER TABLE "OrganizationAiWallets" ALTER COLUMN "Balance" TYPE numeric(24,6);
            ALTER TABLE "AiCreditTransactions" ALTER COLUMN "Amount" TYPE numeric(24,6),
                ALTER COLUMN "BalanceBefore" TYPE numeric(24,6), ALTER COLUMN "BalanceAfter" TYPE numeric(24,6);
            ALTER TABLE "AiCreditReservations" ALTER COLUMN "ReservedAmountRial" TYPE numeric(24,6),
                ALTER COLUMN "ConsumedAmountRial" TYPE numeric(24,6);
            ALTER TABLE "InterviewBillings" ALTER COLUMN "BilledAmountRial" TYPE numeric(24,6),
                ADD COLUMN "RatePerMinuteRial" numeric(24,6) NOT NULL DEFAULT 0,
                ADD COLUMN "RialPerUsdt" numeric(24,6) NOT NULL DEFAULT 0,
                ADD COLUMN "RateQuotedAtUtc" timestamptz NOT NULL DEFAULT now();
            ALTER TABLE "RegistrationTrials" ALTER COLUMN "CreditAmountRial" TYPE numeric(24,6);
            ALTER TABLE "AiCreditPackages" ALTER COLUMN "CreditAmountRial" TYPE numeric(24,6);
            ALTER TABLE "Payments" ALTER COLUMN "CreditAmountRialSnapshot" TYPE numeric(24,6),
                ALTER COLUMN "EffectiveCreditRialToIrtSnapshot" TYPE numeric(30,12);

            DO $$ DECLARE rate numeric := current_setting('hireyar.ai_credit_cutover_rial_per_usd')::numeric;
            BEGIN
                UPDATE "OrganizationAiWallets" SET "Balance" = "Balance" * rate, "Currency" = 'IRR';
                UPDATE "AiCreditTransactions" SET "AmountUsd" = CASE WHEN "Type" = 'InterviewUsage' THEN "Amount" ELSE NULL END, "Amount" = "Amount" * rate,
                    "BalanceBefore" = "BalanceBefore" * rate, "BalanceAfter" = "BalanceAfter" * rate;
                UPDATE "AiCreditReservations" SET "ReservedAmountRial" = "ReservedAmountRial" * rate,
                    "ConsumedAmountRial" = "ConsumedAmountRial" * rate;
                UPDATE "InterviewBillings" SET "BilledAmountRial" = "BilledAmountUsd" * rate,
                    "RatePerMinuteRial" = "RatePerMinuteUsd" * rate, "RialPerUsdt" = rate, "RateQuotedAtUtc" = now();
                UPDATE "RegistrationTrials" SET "CreditAmountRial" = "CreditAmountRial" * rate;
                UPDATE "AiCreditPackages" SET "CreditAmountRial" = "CreditAmountRial" * rate,
                    "RowVersion" = decode(md5("Id"::text || now()::text), 'hex');
                UPDATE "Payments" SET "CreditAmountRialSnapshot" = "CreditAmountRialSnapshot" * rate,
                    "EffectiveCreditRialToIrtSnapshot" = "EffectiveCreditRialToIrtSnapshot" / rate
                    WHERE "CreditAmountRialSnapshot" IS NOT NULL;
            END $$;
            ALTER TABLE "InterviewBillings" ALTER COLUMN "BilledAmountRial" DROP DEFAULT, ALTER COLUMN "RatePerMinuteRial" DROP DEFAULT,
                ALTER COLUMN "RialPerUsdt" DROP DEFAULT, ALTER COLUMN "RateQuotedAtUtc" DROP DEFAULT;
            ALTER TABLE "OrganizationAiWallets" ADD CONSTRAINT "CK_Wallet_Values" CHECK ("Balance" >= 0 AND "Currency" = 'IRR');
            ALTER TABLE "InterviewBillings" ADD CONSTRAINT "CK_InterviewBilling_Values" CHECK
                ("RatePerMinuteUsd" > 0 AND "RatePerMinuteRial" > 0 AND "RialPerUsdt" > 0 AND
                 "BillableSeconds" BETWEEN 0 AND 3600 AND "BilledAmountRial" >= 0 AND "Status" IN ('Reserved','Settled','Released'));
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "OrganizationAiWallets" w WHERE w."Balance" <>
                    (SELECT COALESCE(SUM(t."Amount"),0) FROM "AiCreditTransactions" t WHERE t."WalletId" = w."Id" AND t."OrganizationId" = w."OrganizationId")
                    OR w."Balance" < (SELECT COALESCE(SUM(r."ReservedAmountRial"),0) FROM "AiCreditReservations" r
                        WHERE r."WalletId" = w."Id" AND r."OrganizationId" = w."OrganizationId" AND r."Status" = 'Active')) THEN
                    RAISE EXCEPTION 'AI credit cutover ledger mismatch';
                END IF;
            END $$;
            """);
        // Historical migration constants stay untouched. Only current trigger functions change column names.
        foreach (var function in new[] { "hireyar_interview_billing_guard", "hireyar_reservation_guard", "hireyar_wallet_matches_ledger" })
        {
            var sql = Regex.Match(BillingSeed.Guards, $@"CREATE FUNCTION {function}\(\)[\s\S]*?END \$\$;").Value;
            migrationBuilder.Sql(sql.Replace("CREATE FUNCTION", "CREATE OR REPLACE FUNCTION")
                .Replace("'BilledAmountUsd'", "'BilledAmountRial','BilledAmountUsd'").Replace("ConsumedAmountUsd", "ConsumedAmountRial")
                .Replace("ReservedAmountUsd", "ReservedAmountRial"));
        }
        migrationBuilder.Sql("""
            -- Rewrite references in later financial integrity functions too (including trial completion).
            DO $$ DECLARE fn record; definition text; changed text;
            BEGIN
                FOR fn IN SELECT p.oid FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                    WHERE n.nspname = current_schema() AND p.proname LIKE 'hireyar_%' AND p.prokind = 'f' LOOP
                    definition := pg_get_functiondef(fn.oid);
                    changed := replace(replace(replace(replace(definition,
                        'CreditAmountUsd', 'CreditAmountRial'), 'EffectiveUsdToIrtSnapshot', 'EffectiveCreditRialToIrtSnapshot'),
                        'ReservedAmountUsd', 'ReservedAmountRial'), 'ConsumedAmountUsd', 'ConsumedAmountRial');
                    IF changed <> definition THEN EXECUTE changed; END IF;
                END LOOP;
            END $$;
            ALTER TABLE "OrganizationAiWallets" ENABLE TRIGGER USER;
            ALTER TABLE "AiCreditTransactions" ENABLE TRIGGER USER;
            ALTER TABLE "AiCreditReservations" ENABLE TRIGGER USER;
            ALTER TABLE "InterviewBillings" ENABLE TRIGGER USER;
            ALTER TABLE "Payments" ENABLE TRIGGER USER;
            ALTER TABLE "RegistrationTrials" ENABLE TRIGGER USER;
            ALTER TABLE "AiCreditPackages" ENABLE TRIGGER USER;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN RAISE EXCEPTION 'AI credit currency cutover cannot be downgraded. Restore the pre-cutover backup instead.'; END $$;
        """);
}
