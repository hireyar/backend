using Microsoft.EntityFrameworkCore.Migrations;
namespace HireYar.Infrastructure.Persistence.Migrations;
internal static class SalesGuards
{
    internal static void Up(MigrationBuilder m) => m.Sql("""
        CREATE FUNCTION sales_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'sales history is immutable' USING ERRCODE='23514'; END $$;
        CREATE TRIGGER sales_audit_immutable BEFORE UPDATE OR DELETE ON "SalesAudits" FOR EACH ROW EXECUTE FUNCTION sales_immutable();
        CREATE FUNCTION sales_commission_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' THEN RAISE EXCEPTION 'sales commission is immutable' USING ERRCODE='23514'; END IF;
          IF (to_jsonb(NEW)-'PayoutId') IS DISTINCT FROM (to_jsonb(OLD)-'PayoutId') OR OLD."PayoutId" IS NOT NULL OR NEW."PayoutId" IS NULL
          THEN RAISE EXCEPTION 'sales commission is immutable' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER sales_commission_immutable BEFORE UPDATE OR DELETE ON "SalesCommissions" FOR EACH ROW EXECUTE FUNCTION sales_commission_guard();
        CREATE FUNCTION sales_payout_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' THEN RAISE EXCEPTION 'sales payout is immutable' USING ERRCODE='23514'; END IF;
          IF OLD."Status" <> 'Requested' OR NEW."Status" NOT IN ('Paid','Returned') OR
             (to_jsonb(NEW)-ARRAY['Status','ReviewedAtUtc','ReviewedById','ReviewNote']) IS DISTINCT FROM
             (to_jsonb(OLD)-ARRAY['Status','ReviewedAtUtc','ReviewedById','ReviewNote'])
          THEN RAISE EXCEPTION 'invalid sales payout transition' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER sales_payout_transition BEFORE UPDATE OR DELETE ON "SalesPayouts" FOR EACH ROW EXECUTE FUNCTION sales_payout_guard();
        CREATE FUNCTION sales_lead_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' THEN RAISE EXCEPTION 'sales lead history is retained' USING ERRCODE='23514'; END IF;
          IF NEW."Id"<>OLD."Id" OR NEW."Mobile"<>OLD."Mobile" OR NEW."CreatedAtUtc"<>OLD."CreatedAtUtc" OR
             (OLD."OrganizationId" IS NOT NULL AND (NEW."OrganizationId" IS DISTINCT FROM OLD."OrganizationId" OR NEW."RegisteredAtUtc" IS DISTINCT FROM OLD."RegisteredAtUtc"))
          THEN RAISE EXCEPTION 'sales attribution is immutable' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER sales_lead_attribution BEFORE UPDATE OR DELETE ON "SalesLeads" FOR EACH ROW EXECUTE FUNCTION sales_lead_guard();
        CREATE FUNCTION sales_validate_commission() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE c "SalesCommissions"; o "SubscriptionOrders"; l "SalesLeads"; p "SalesPayouts";
        BEGIN
          SELECT * INTO c FROM "SalesCommissions" WHERE "Id"=NEW."Id";
          SELECT * INTO o FROM "SubscriptionOrders" WHERE "Id"=c."OrderId";
          SELECT * INTO l FROM "SalesLeads" WHERE "Id"=c."LeadId";
          IF l."OrganizationId" IS DISTINCT FROM c."OrganizationId" OR o."OrganizationId"<>c."OrganizationId" OR
             o."Status"<>'Confirmed' OR o."Product"<>'subscription' OR o."AmountRial"<>c."InvoiceAmountRial" OR o."ConfirmedAtUtc"<>c."CreatedAtUtc"
          THEN RAISE EXCEPTION 'invalid sales commission source' USING ERRCODE='23514'; END IF;
          IF TG_OP='INSERT' AND c."AgentId"<>l."AgentId" THEN RAISE EXCEPTION 'invalid sales commission owner' USING ERRCODE='23514'; END IF;
          IF c."PayoutId" IS NOT NULL THEN
            SELECT * INTO p FROM "SalesPayouts" WHERE "Id"=c."PayoutId";
            IF p."AgentId"<>c."AgentId" OR p."CreatedAtUtc"<c."AvailableAtUtc" OR c."AmountRial"<=0 OR p."AmountRial" IS DISTINCT FROM (SELECT sum("AmountRial") FROM "SalesCommissions" WHERE "PayoutId"=p."Id")
            THEN RAISE EXCEPTION 'invalid sales payout eligibility' USING ERRCODE='23514'; END IF;
          END IF;
          RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER sales_commission_source AFTER INSERT OR UPDATE ON "SalesCommissions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sales_validate_commission();
        CREATE FUNCTION sales_validate_payout() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE amount bigint;
        BEGIN
          SELECT sum("AmountRial") INTO amount FROM "SalesCommissions" WHERE "PayoutId"=NEW."Id" AND "AgentId"=NEW."AgentId";
          IF amount IS DISTINCT FROM NEW."AmountRial" THEN RAISE EXCEPTION 'invalid sales payout total' USING ERRCODE='23514'; END IF;
          RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER sales_payout_total AFTER INSERT ON "SalesPayouts" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sales_validate_payout();
        """);
    internal static void Down(MigrationBuilder m) => m.Sql("""
        DO $$ BEGIN
          IF EXISTS(SELECT 1 FROM "SalesAgents") OR EXISTS(SELECT 1 FROM "SalesLeads") OR EXISTS(SELECT 1 FROM "SalesCommissions") OR EXISTS(SELECT 1 FROM "SalesPayouts") OR EXISTS(SELECT 1 FROM "SalesAudits")
          THEN RAISE EXCEPTION 'sales history requires backup; rollback refused'; END IF;
        END $$;
        DROP TRIGGER sales_audit_immutable ON "SalesAudits";
        DROP TRIGGER sales_commission_immutable ON "SalesCommissions";
        DROP TRIGGER sales_payout_transition ON "SalesPayouts";
        DROP TRIGGER sales_lead_attribution ON "SalesLeads";
        DROP TRIGGER sales_commission_source ON "SalesCommissions";
        DROP TRIGGER sales_payout_total ON "SalesPayouts";
        DROP FUNCTION sales_immutable(), sales_commission_guard(), sales_payout_guard(), sales_lead_guard(), sales_validate_commission(), sales_validate_payout();
        """);
}
