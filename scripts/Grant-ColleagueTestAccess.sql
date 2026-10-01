-- PostgreSQL. Apply AddPricingBilling before running this entire file.
-- One grant per organization of currently active users (shared organization wallet).
-- InternalTestGrant is a NON-CASH tester grant, never a verified gateway payment.
-- Its nominal catalog amount MUST be excluded from cash/revenue reporting.
-- Keep campaign unchanged for retries; change it only for an intentional NEW grant.
BEGIN;
LOCK TABLE "PricingPackages", "PackagePeriods", "PackageFeatures", "FeatureDefinitions" IN SHARE MODE;

CREATE TEMP TABLE colleague_grant_result (
    organization_id uuid, credit_added_usd numeric, balance_usd numeric,
    subscription_id uuid, subscription_expires_at timestamptz
) ON COMMIT DROP;

DO $grant$
DECLARE
    credit_usd numeric := 100.000000; -- EDIT: additional credit per organization
    period_months integer := 1;      -- EDIT: catalog period, usually 1/3/6/12
    campaign text := 'colleague-test-2026-10-01-v1';
    at_utc timestamptz := transaction_timestamp();
    pkg "PricingPackages"%ROWTYPE;
    period "PackagePeriods"%ROWTYPE;
    org record;
    wallet "OrganizationAiWallets"%ROWTYPE;
    existing_credit "AiCreditTransactions"%ROWTYPE;
    active_sub "OrganizationSubscriptions"%ROWTYPE;
    reference_id uuid;
    payment_id uuid;
    purchase_id uuid;
    new_sub_id uuid;
    expires_at timestamptz;
    added numeric;
BEGIN
    IF credit_usd <= 0 OR credit_usd <> round(credit_usd, 6) THEN
        RAISE EXCEPTION 'Credit must be positive USD with at most six decimal places';
    END IF;
    SELECT * INTO STRICT pkg FROM "PricingPackages" WHERE "Code" = 'advanced' AND "IsActive";
    SELECT * INTO STRICT period FROM "PackagePeriods"
      WHERE "PackageId" = pkg."Id" AND "Months" = period_months AND "IsActive";

    FOR org IN
        SELECT o."Id" FROM "Organizations" o
        WHERE EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u."OrganizationId" = o."Id" AND u."IsActive")
        ORDER BY o."Id" FOR UPDATE OF o
    LOOP
        reference_id := md5(campaign || ':credit:' || org."Id"::text)::uuid;
        payment_id := md5(campaign || ':payment:' || org."Id"::text)::uuid;
        purchase_id := md5(campaign || ':purchase:' || org."Id"::text)::uuid;
        new_sub_id := md5(campaign || ':subscription:' || org."Id"::text)::uuid;

        SELECT * INTO active_sub FROM "OrganizationSubscriptions"
          WHERE "OrganizationId" = org."Id" AND "Status" IN ('Active', 'Scheduled')
            AND "StartedAtUtc" <= at_utc AND "ExpiresAtUtc" > at_utc;
        IF FOUND THEN
            IF active_sub."PackageId" <> pkg."Id" THEN
                RAISE EXCEPTION 'Organization % has another active package; no changes committed', org."Id";
            END IF;
            -- Reuse its existing purchased Advanced contract; do not overwrite history.
        ELSE
            expires_at := at_utc + make_interval(months => period."Months");
            IF EXISTS (SELECT 1 FROM "OrganizationSubscriptions"
                WHERE "OrganizationId" = org."Id" AND "Status" IN ('Active', 'Scheduled')
                  AND "StartedAtUtc" < expires_at AND "ExpiresAtUtc" > at_utc) THEN
                RAISE EXCEPTION 'Organization % has an overlapping scheduled subscription; no changes committed', org."Id";
            END IF;
            IF EXISTS (SELECT 1 FROM "Payments" WHERE "Id" = payment_id) THEN
                RAISE EXCEPTION 'Test grant already used for organization %; use a new campaign for a new subscription', org."Id";
            END IF;

            INSERT INTO "Payments" ("Id", "OrganizationId", "Type", "Amount", "Currency", "Status",
                "Provider", "ProviderTransactionId", "CreatedAtUtc", "ExpiresAtUtc", "PaidAtUtc")
            VALUES (payment_id, org."Id", 'Subscription', period."PriceAmount", period."Currency", 'Paid',
                'InternalTestGrant', payment_id::text, at_utc, expires_at, at_utc);

            INSERT INTO "SubscriptionPurchases" ("Id", "OrganizationId", "PaymentId", "PackageId", "PackagePeriodId",
                "PackageCodeSnapshot", "PackageNameSnapshot", "PeriodMonthsSnapshot", "PriceAmountSnapshot",
                "CurrencySnapshot", "DiscountPercentSnapshot", "AiPricePerMinuteUsdSnapshot", "RetentionDaysSnapshot", "CreatedAtUtc")
            VALUES (purchase_id, org."Id", payment_id, pkg."Id", period."Id", pkg."Code", pkg."Name",
                period."Months", period."PriceAmount", period."Currency", period."DiscountPercent",
                pkg."AiPricePerMinuteUsd", pkg."InterviewRetentionDays", at_utc);

            INSERT INTO "SubscriptionEntitlement" ("PurchaseId", "Code", "Name", "DataType", "Value")
            SELECT purchase_id, f."Code", f."Name", f."DataType", pf."Value"
            FROM "PackageFeatures" pf JOIN "FeatureDefinitions" f ON f."Id" = pf."FeatureId"
            WHERE pf."PackageId" = pkg."Id" AND pf."IsEnabled";

            INSERT INTO "OrganizationSubscriptions" ("Id", "OrganizationId", "PaymentId", "PackageId", "PackagePeriodId",
                "Status", "StartedAtUtc", "ExpiresAtUtc", "CreatedAtUtc")
            VALUES (new_sub_id, org."Id", payment_id, pkg."Id", period."Id", 'Active', at_utc, expires_at, at_utc)
            RETURNING * INTO active_sub;
        END IF;

        INSERT INTO "OrganizationAiWallets" ("Id", "OrganizationId", "Currency", "Balance", "CreatedAtUtc", "UpdatedAtUtc")
        VALUES (gen_random_uuid(), org."Id", 'USD', 0, at_utc, at_utc)
        ON CONFLICT ("OrganizationId") DO NOTHING;
        SELECT * INTO STRICT wallet FROM "OrganizationAiWallets" WHERE "OrganizationId" = org."Id" FOR UPDATE;

        added := 0;
        SELECT * INTO existing_credit FROM "AiCreditTransactions"
          WHERE "OrganizationId" = org."Id" AND "ReferenceType" = 'ColleagueTestGrant' AND "ReferenceId" = reference_id;
        IF FOUND THEN
            IF existing_credit."Amount" <> credit_usd THEN
                RAISE EXCEPTION 'Campaign credit amount changed; use the original amount or a new campaign';
            END IF;
        ELSE
            INSERT INTO "AiCreditTransactions" ("Id", "OrganizationId", "WalletId", "Type", "Amount",
                "BalanceBefore", "BalanceAfter", "ReferenceType", "ReferenceId", "Description", "CreatedAtUtc")
            VALUES (gen_random_uuid(), org."Id", wallet."Id", 'PromotionalCredit', credit_usd,
                wallet."Balance", wallet."Balance" + credit_usd, 'ColleagueTestGrant', reference_id,
                'Non-cash colleague testing credit: ' || campaign, at_utc);
            UPDATE "OrganizationAiWallets" SET "Balance" = "Balance" + credit_usd, "UpdatedAtUtc" = at_utc
              WHERE "Id" = wallet."Id" RETURNING * INTO wallet;
            added := credit_usd;
        END IF;
        INSERT INTO colleague_grant_result VALUES
          (org."Id", added, wallet."Balance", active_sub."Id", active_sub."ExpiresAtUtc");
    END LOOP;
END $grant$;

-- Force deferred ledger validation before displaying the results.
SET CONSTRAINTS ALL IMMEDIATE;
SELECT * FROM colleague_grant_result ORDER BY organization_id;
COMMIT;
