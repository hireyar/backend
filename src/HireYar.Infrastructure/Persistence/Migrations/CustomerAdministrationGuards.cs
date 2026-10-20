using Microsoft.EntityFrameworkCore.Migrations;
namespace HireYar.Infrastructure.Persistence.Migrations;
internal static class CustomerAdministrationGuards
{
    private const string PreviousCommission = """
        CREATE OR REPLACE FUNCTION sales_validate_commission() RETURNS trigger LANGUAGE plpgsql AS $$
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
        """;
    private const string PreviousLead = """
        CREATE OR REPLACE FUNCTION sales_lead_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' THEN RAISE EXCEPTION 'sales lead history is retained' USING ERRCODE='23514'; END IF;
          IF NEW."Id"<>OLD."Id" OR NEW."Mobile"<>OLD."Mobile" OR NEW."CreatedAtUtc"<>OLD."CreatedAtUtc" OR
             (OLD."OrganizationId" IS NOT NULL AND (NEW."OrganizationId" IS DISTINCT FROM OLD."OrganizationId" OR NEW."RegisteredAtUtc" IS DISTINCT FROM OLD."RegisteredAtUtc"))
          THEN RAISE EXCEPTION 'sales attribution is immutable' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        """;
    public static void Up(MigrationBuilder m)
    {
        m.Sql(PreviousCommission.Replace("c.\"AgentId\"<>l.\"AgentId\"", "c.\"AgentId\" IS DISTINCT FROM l.\"AgentId\""));
        m.Sql(PreviousLead.Replace(" OR NEW.\"Mobile\"<>OLD.\"Mobile\"", "").Replace("(NEW.\"OrganizationId\" IS DISTINCT", "(NEW.\"Mobile\"<>OLD.\"Mobile\" OR NEW.\"OrganizationId\" IS DISTINCT"));
        m.Sql("""
            CREATE FUNCTION customer_admin_event_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'customer administration history is immutable' USING ERRCODE='23514'; END IF;
              IF NOT EXISTS(SELECT 1 FROM "AspNetUsers" WHERE "Id"=NEW."ActorId" AND "IsSystemAdmin" AND "IsActive")
              THEN RAISE EXCEPTION 'invalid customer administration actor' USING ERRCODE='23514'; END IF;
              IF NEW."CustomerKind"='User' AND NOT EXISTS(SELECT 1 FROM "AspNetUsers" WHERE "Id"=NEW."CustomerId" AND "OrganizationId"=NEW."OrganizationId" AND "Role"='Owner') OR
                 NEW."CustomerKind"='Lead' AND NOT EXISTS(SELECT 1 FROM "SalesLeads" WHERE "Id"=NEW."CustomerId" AND "OrganizationId" IS NOT DISTINCT FROM NEW."OrganizationId")
              THEN RAISE EXCEPTION 'invalid customer administration scope' USING ERRCODE='23514'; END IF;
              IF NEW."CreditQuantity">0 AND NOT EXISTS(SELECT 1 FROM "InterviewBundles" WHERE "Code"=NEW."BundleCode" AND "IsActive")
              THEN RAISE EXCEPTION 'invalid administrative interview bundle' USING ERRCODE='23514'; END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER customer_admin_event_guard BEFORE INSERT OR UPDATE OR DELETE ON "CustomerAdministrationEvents" FOR EACH ROW EXECUTE FUNCTION customer_admin_event_guard();
            CREATE FUNCTION customer_override_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP='DELETE' THEN RAISE EXCEPTION 'customer override history is retained' USING ERRCODE='23514'; END IF;
              IF NOT EXISTS(SELECT 1 FROM "CustomerAdministrationEvents" WHERE "Id"=NEW."EventId" AND "OrganizationId"=NEW."OrganizationId" AND "Action" IN ('SubscriptionSet','SubscriptionDaysAdjusted','SubscriptionStopped'))
              THEN RAISE EXCEPTION 'invalid administrative subscription source' USING ERRCODE='23514'; END IF;
              IF EXISTS(SELECT 1 FROM unnest(NEW."ExcludedOrderIds") x WHERE NOT EXISTS(SELECT 1 FROM "OrganizationMemberships" WHERE "OrderId"=x AND "OrganizationId"=NEW."OrganizationId")) OR
                 EXISTS(SELECT 1 FROM "OrganizationMemberships" WHERE "OrganizationId"=NEW."OrganizationId" AND NOT ("OrderId"=ANY(NEW."ExcludedOrderIds")))
              THEN RAISE EXCEPTION 'invalid administrative subscription exclusions' USING ERRCODE='23514'; END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER customer_override_guard BEFORE INSERT OR UPDATE OR DELETE ON "CustomerSubscriptionOverrides" FOR EACH ROW EXECUTE FUNCTION customer_override_guard();
            """);
    }
    public static void Down(MigrationBuilder m)
    {
        m.Sql("""
            DO $$ BEGIN
              IF EXISTS(SELECT 1 FROM "CustomerAdministrationEvents") OR EXISTS(SELECT 1 FROM "CustomerSubscriptionOverrides") OR
                 EXISTS(SELECT 1 FROM "SalesLeads" WHERE "AgentId" IS NULL OR "ContactEmail" IS NOT NULL OR "ContactOrganizationName"<>'')
              THEN RAISE EXCEPTION 'customer administration history requires backup; rollback refused'; END IF;
            END $$;
            DROP TRIGGER customer_override_guard ON "CustomerSubscriptionOverrides";
            DROP TRIGGER customer_admin_event_guard ON "CustomerAdministrationEvents";
            DROP FUNCTION customer_override_guard(), customer_admin_event_guard();
            """);
        m.Sql(PreviousCommission); m.Sql(PreviousLead);
    }
}
