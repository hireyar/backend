namespace HireYar.Infrastructure.Persistence;

internal static class SubscriptionReplacementGuards
{
    public const string Up = """
        CREATE TRIGGER replacement_immutable BEFORE UPDATE OR DELETE ON "SubscriptionReplacements"
        FOR EACH ROW EXECUTE FUNCTION hireyar_billing_immutable();

        CREATE FUNCTION hireyar_replacement_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          PERFORM 1 FROM "Organizations" WHERE "Id" = NEW."OrganizationId" FOR UPDATE;
          IF NOT EXISTS (
            SELECT 1 FROM "Payments" p
            JOIN "SubscriptionPurchases" q ON q."PaymentId" = p."Id" AND q."OrganizationId" = p."OrganizationId"
            JOIN "OrganizationSubscriptions" s ON s."Id" = NEW."SubscriptionId" AND s."OrganizationId" = NEW."OrganizationId"
            WHERE p."Id" = NEW."PaymentId" AND p."OrganizationId" = NEW."OrganizationId"
              AND p."Status" = 'Paid' AND q."Operation" = 'upgrade' AND q."ReplacesSubscriptionId" = s."Id"
              AND NEW."EndedAtUtc" = p."PaidAtUtc" AND NEW."EndedAtUtc" >= s."StartedAtUtc"
          ) THEN RAISE EXCEPTION 'Invalid subscription replacement' USING ERRCODE = '23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER replacement_guard BEFORE INSERT ON "SubscriptionReplacements" FOR EACH ROW EXECUTE FUNCTION hireyar_replacement_guard();

        CREATE FUNCTION hireyar_replacement_fulfilled() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF NOT EXISTS (SELECT 1 FROM "OrganizationSubscriptions" s WHERE s."OrganizationId" = NEW."OrganizationId"
              AND s."PaymentId" = NEW."PaymentId" AND s."StartedAtUtc" = NEW."EndedAtUtc") THEN
            RAISE EXCEPTION 'Replacement subscription missing' USING ERRCODE = '23514';
          END IF;
          RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER replacement_fulfilled AFTER INSERT ON "SubscriptionReplacements"
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hireyar_replacement_fulfilled();

        CREATE OR REPLACE FUNCTION hireyar_subscription_no_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          PERFORM 1 FROM "Organizations" WHERE "Id" = NEW."OrganizationId" FOR UPDATE;
          IF EXISTS (SELECT 1 FROM "OrganizationSubscriptions" s LEFT JOIN "SubscriptionReplacements" r
              ON r."OrganizationId" = s."OrganizationId" AND r."SubscriptionId" = s."Id"
            WHERE s."OrganizationId" = NEW."OrganizationId"
              AND s."Status" IN ('Active','Scheduled') AND NEW."Status" IN ('Active','Scheduled')
              AND s."StartedAtUtc" < NEW."ExpiresAtUtc"
              AND LEAST(s."ExpiresAtUtc", COALESCE(r."EndedAtUtc", s."ExpiresAtUtc")) > NEW."StartedAtUtc") THEN
            RAISE EXCEPTION 'Subscription overlap' USING ERRCODE = '23514';
          END IF;
          RETURN NEW;
        END $$;
        """;
    public const string Down = """
        DROP TRIGGER replacement_immutable ON "SubscriptionReplacements";
        DROP FUNCTION hireyar_replacement_guard() CASCADE;
        DROP FUNCTION hireyar_replacement_fulfilled() CASCADE;
        CREATE OR REPLACE FUNCTION hireyar_subscription_no_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          PERFORM 1 FROM "Organizations" WHERE "Id" = NEW."OrganizationId" FOR UPDATE;
          IF EXISTS (SELECT 1 FROM "OrganizationSubscriptions" WHERE "OrganizationId" = NEW."OrganizationId"
            AND "Status" IN ('Active','Scheduled') AND NEW."Status" IN ('Active','Scheduled')
            AND "StartedAtUtc" < NEW."ExpiresAtUtc" AND "ExpiresAtUtc" > NEW."StartedAtUtc") THEN
            RAISE EXCEPTION 'Subscription overlap' USING ERRCODE = '23514';
          END IF;
          RETURN NEW;
        END $$;
        """;
    // Only untouched seed prices change. Pending/paid contracts and administrator edits retain their values.
    public const string CatalogUpdate = """
        UPDATE "PackagePeriods" p SET "PriceAmount" = v.new_price, "DiscountPercent" = v.new_discount, "UpdatedAtUtc" = now()
        FROM (VALUES
          ('bd000000-0000-0000-0000-000000000001'::uuid, 3, 8100000, 10, 8550000, 5),
          ('bd000000-0000-0000-0000-000000000001'::uuid, 6, 15300000, 15, 16200000, 10),
          ('bd000000-0000-0000-0000-000000000002'::uuid, 3, 12150000, 10, 12825000, 5),
          ('bd000000-0000-0000-0000-000000000002'::uuid, 6, 22950000, 15, 24300000, 10)
        ) AS v(package_id, months, old_price, old_discount, new_price, new_discount)
        WHERE p."PackageId" = v.package_id AND p."Months" = v.months AND p."PriceAmount" = v.old_price AND p."DiscountPercent" = v.old_discount;
        UPDATE "PricingPackages" SET "RowVersion" = decode(md5("Id"::text || '-subscription-credit-panel'), 'hex'), "UpdatedAtUtc" = now()
        WHERE "Id" IN ('bd000000-0000-0000-0000-000000000001', 'bd000000-0000-0000-0000-000000000002');
        """;
}
