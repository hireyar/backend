namespace HireYar.Infrastructure.Persistence;

// One-time migration seed. Never runs on startup or updates administrator-modified catalog rows.
internal static class BillingSeed
{
    public const string Sql = """
        INSERT INTO "PricingPackages" ("Id", "Code", "Name", "Description", "IsActive", "IsPublic", "SortOrder", "AiPricePerMinuteUsd", "InterviewRetentionDays", "CreatedAtUtc", "UpdatedAtUtc", "RowVersion")
        VALUES
        ('bd000000-0000-0000-0000-000000000001', 'basic', 'پکیج پایه', 'مصاحبه عمومی و غربالگری اولیه', true, true, 1, 0.03, 30, now(), now(), decode(md5('billing-basic'), 'hex')),
        ('bd000000-0000-0000-0000-000000000002', 'advanced', 'پکیج پیشرفته', 'مصاحبه فنی و تحلیل تطبیقی', true, true, 2, 0.05, 60, now(), now(), decode(md5('billing-advanced'), 'hex'))
        ON CONFLICT ("Code") DO NOTHING;

        INSERT INTO "PackagePeriods" ("Id", "PackageId", "Months", "PriceAmount", "Currency", "DiscountPercent", "IsActive", "SortOrder", "CreatedAtUtc", "UpdatedAtUtc")
        SELECT md5(p."Code" || '-billing-period-' || v.months)::uuid, p."Id", v.months, v.price, 'IRT', v.discount, true, v.months, now(), now()
        FROM "PricingPackages" p JOIN (VALUES
          ('basic', 1, 3000000, 0), ('basic', 3, 8100000, 10), ('basic', 6, 15300000, 15), ('basic', 12, 28800000, 20),
          ('advanced', 1, 4500000, 0), ('advanced', 3, 12150000, 10), ('advanced', 6, 22950000, 15), ('advanced', 12, 43200000, 20)
        ) AS v(code, months, price, discount) ON p."Code" = v.code
        WHERE p."Id" IN ('bd000000-0000-0000-0000-000000000001', 'bd000000-0000-0000-0000-000000000002')
        ON CONFLICT ("PackageId", "Months") DO NOTHING;

        INSERT INTO "FeatureDefinitions" ("Id", "Code", "Name", "Description", "DataType")
        SELECT md5('billing-feature-' || v.code)::uuid, v.code, v.name, '', 'Boolean'
        FROM (VALUES
          ('hr_interview', 'مصاحبه عمومی HR'), ('candidate_screening', 'غربالگری اولیه داوطلب'),
          ('interview_report', 'گزارش مصاحبه'), ('interview_result', 'نتیجه مصاحبه'), ('support', 'پشتیبانی'), ('ai_interview', 'مصاحبه با کمک AI'),
          ('technical_interview', 'مصاحبه فنی'), ('answer_analysis', 'تحلیل پاسخ داوطلب'), ('adaptive_questions', 'انتخاب تطبیقی سؤال'),
          ('adaptive_difficulty', 'تنظیم سختی سؤال'), ('avoid_repeated_topics', 'پرهیز از موضوع تکراری'), ('skill_profile', 'پروفایل مهارت'),
          ('technical_report', 'گزارش ارزیابی فنی'), ('technical_support', 'پشتیبانی فنی'), ('advanced_ai_workflow', 'گردش کار پیشرفته AI')
        ) AS v(code, name) ON CONFLICT ("Code") DO NOTHING;

        INSERT INTO "PackageFeatures" ("PackageId", "FeatureId", "IsEnabled", "Value")
        SELECT p."Id", f."Id", true, NULL FROM "PricingPackages" p CROSS JOIN "FeatureDefinitions" f
        WHERE p."Id" IN ('bd000000-0000-0000-0000-000000000001', 'bd000000-0000-0000-0000-000000000002')
          AND f."Code" IN ('hr_interview','candidate_screening','interview_report','interview_result','support','ai_interview',
            'technical_interview','answer_analysis','adaptive_questions','adaptive_difficulty','avoid_repeated_topics','skill_profile','technical_report','technical_support','advanced_ai_workflow')
          AND (p."Code" = 'advanced' OR f."Code" IN ('hr_interview','candidate_screening','interview_report','interview_result','support','ai_interview'))
        ON CONFLICT ("PackageId", "FeatureId") DO NOTHING;
        """;
    public const string Guards = """
        CREATE FUNCTION hireyar_billing_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Financial history is immutable' USING ERRCODE = '23514'; END $$;
        CREATE TRIGGER ledger_immutable BEFORE UPDATE OR DELETE ON "AiCreditTransactions" FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();
        CREATE TRIGGER purchase_immutable BEFORE UPDATE OR DELETE ON "SubscriptionPurchases" FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();
        CREATE TRIGGER entitlement_immutable BEFORE UPDATE OR DELETE ON "SubscriptionEntitlement" FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();
        CREATE TRIGGER subscription_immutable BEFORE UPDATE OR DELETE ON "OrganizationSubscriptions" FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();

        CREATE FUNCTION hireyar_subscription_no_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          PERFORM 1 FROM "Organizations" WHERE "Id" = NEW."OrganizationId" FOR UPDATE;
          IF EXISTS (SELECT 1 FROM "OrganizationSubscriptions" WHERE "OrganizationId" = NEW."OrganizationId"
            AND "Status" IN ('Active','Scheduled') AND NEW."Status" IN ('Active','Scheduled')
            AND "StartedAtUtc" < NEW."ExpiresAtUtc" AND "ExpiresAtUtc" > NEW."StartedAtUtc") THEN
            RAISE EXCEPTION 'Subscription overlap' USING ERRCODE = '23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER subscription_no_overlap BEFORE INSERT ON "OrganizationSubscriptions" FOR EACH ROW EXECUTE FUNCTION hireyar_subscription_no_overlap();

        CREATE FUNCTION hireyar_payment_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Payment history is immutable' USING ERRCODE = '23514'; END IF;
          IF OLD."Status" = 'Paid' OR
            (to_jsonb(NEW) - ARRAY['Status','ProviderAuthority','ProviderTransactionId','PaidAtUtc','FailedAtUtc']) <>
            (to_jsonb(OLD) - ARRAY['Status','ProviderAuthority','ProviderTransactionId','PaidAtUtc','FailedAtUtc']) THEN
            RAISE EXCEPTION 'Payment quote is immutable' USING ERRCODE = '23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER payment_guard BEFORE UPDATE OR DELETE ON "Payments" FOR EACH ROW EXECUTE FUNCTION hireyar_payment_guard();

        CREATE FUNCTION hireyar_interview_billing_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Billing history is immutable' USING ERRCODE = '23514'; END IF;
          IF OLD."Status" <> 'Reserved' OR
             (to_jsonb(NEW) - ARRAY['Status','BillableSeconds','BilledAmountUsd','SettledAtUtc']) <>
             (to_jsonb(OLD) - ARRAY['Status','BillableSeconds','BilledAmountUsd','SettledAtUtc']) THEN
            RAISE EXCEPTION 'Interview billing snapshot is immutable' USING ERRCODE = '23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER interview_billing_guard BEFORE UPDATE OR DELETE ON "InterviewBillings" FOR EACH ROW EXECUTE FUNCTION hireyar_interview_billing_guard();

        CREATE FUNCTION hireyar_reservation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Reservation history is immutable' USING ERRCODE = '23514'; END IF;
          IF OLD."Status" <> 'Active' OR
             (to_jsonb(NEW) - ARRAY['Status','ConsumedAmountUsd']) <>
             (to_jsonb(OLD) - ARRAY['Status','ConsumedAmountUsd']) THEN
            RAISE EXCEPTION 'Reservation snapshot is immutable' USING ERRCODE = '23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER reservation_guard BEFORE UPDATE OR DELETE ON "AiCreditReservations" FOR EACH ROW EXECUTE FUNCTION hireyar_reservation_guard();

        CREATE FUNCTION hireyar_wallet_matches_ledger() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE wallet_id uuid; org_id uuid; cached numeric; ledger numeric; reserved numeric;
        BEGIN
          IF TG_TABLE_NAME = 'OrganizationAiWallets' THEN wallet_id := NEW."Id"; ELSE wallet_id := NEW."WalletId"; END IF;
          org_id := NEW."OrganizationId";
          SELECT "Balance" INTO cached FROM "OrganizationAiWallets" WHERE "Id" = wallet_id AND "OrganizationId" = org_id FOR UPDATE;
          SELECT COALESCE(SUM("Amount"), 0) INTO ledger FROM "AiCreditTransactions" WHERE "WalletId" = wallet_id AND "OrganizationId" = org_id;
          SELECT COALESCE(SUM("ReservedAmountUsd"), 0) INTO reserved FROM "AiCreditReservations" WHERE "WalletId" = wallet_id AND "OrganizationId" = org_id AND "Status" = 'Active';
          IF cached IS NULL OR cached <> ledger OR cached < reserved THEN
            RAISE EXCEPTION 'Wallet ledger or reservation mismatch' USING ERRCODE = '23514';
          END IF;
          RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER wallet_matches_ledger AFTER INSERT OR UPDATE ON "OrganizationAiWallets" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hireyar_wallet_matches_ledger();
        CREATE CONSTRAINT TRIGGER ledger_matches_wallet AFTER INSERT ON "AiCreditTransactions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hireyar_wallet_matches_ledger();
        CREATE CONSTRAINT TRIGGER reservation_matches_wallet AFTER INSERT OR UPDATE ON "AiCreditReservations" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hireyar_wallet_matches_ledger();
        """;
}
