namespace HireYar.Infrastructure.Persistence.Migrations;

internal static class InterviewUsageHistorySql
{
    internal const string Up = """
        CREATE FUNCTION hireyar_usage_event(org uuid, session_id uuid, kind text, occurred timestamptz, historical boolean DEFAULT false)
        RETURNS void LANGUAGE sql AS $fn$
            INSERT INTO "InterviewUsageEvents" ("Id", "OrganizationId", "InterviewSessionId", "EventType", "OccurredAtUtc", "IsHistorical")
            VALUES (gen_random_uuid(), org, session_id, kind, occurred, historical)
            ON CONFLICT ("InterviewSessionId", "EventType") DO NOTHING;
        $fn$;

        CREATE FUNCTION hireyar_usage_session_event() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            IF TG_OP = 'INSERT' THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."Id", 'Created', NEW."CreatedAtUtc");
                RETURN NEW;
            END IF;
            -- Preserve expiration even when a late cancellation/replacement precedes the next worker tick.
            IF OLD."Status" NOT IN ('Completed','Cancelled') AND OLD."AccessRevokedAtUtc" IS NULL AND OLD."DeletedAtUtc" IS NULL
               AND OLD."ExpiresAtUtc" <= COALESCE(NEW."CompletedAtUtc", NEW."AccessRevokedAtUtc", NEW."DeletedAtUtc", transaction_timestamp()) THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."Id", 'Expired', OLD."ExpiresAtUtc");
            END IF;
            IF OLD."StartedAtUtc" IS NULL AND NEW."StartedAtUtc" IS NOT NULL THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."Id", 'Started', NEW."StartedAtUtc");
            END IF;
            IF NEW."Status" IS DISTINCT FROM OLD."Status" AND NEW."Status" IN ('Completed','Cancelled') THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."Id", NEW."Status", NEW."CompletedAtUtc");
            END IF;
            IF OLD."SupersededByInterviewId" IS NULL AND NEW."SupersededByInterviewId" IS NOT NULL THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."Id", 'Replaced', NEW."AccessRevokedAtUtc");
            END IF;
            IF OLD."DeletedAtUtc" IS NULL AND NEW."DeletedAtUtc" IS NOT NULL THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."Id", 'Deleted', NEW."DeletedAtUtc");
            END IF;
            IF OLD."AccessRevokedAtUtc" IS NULL AND NEW."AccessRevokedAtUtc" IS NOT NULL
               AND NEW."SupersededByInterviewId" IS NULL AND NEW."DeletedAtUtc" IS NULL AND NEW."Status" <> 'Cancelled' THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."Id", 'AccessRevoked', NEW."AccessRevokedAtUtc");
            END IF;
            RETURN NEW;
        END; $fn$;
        CREATE TRIGGER interview_usage_session AFTER INSERT OR UPDATE ON "InterviewSessions"
            FOR EACH ROW EXECUTE FUNCTION hireyar_usage_session_event();

        CREATE FUNCTION hireyar_usage_quota_event() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN
            IF TG_OP = 'INSERT' THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."InterviewSessionId", 'Reserved', NEW."CreatedAtUtc");
            END IF;
            IF NEW."State" = 'Consumed' THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."InterviewSessionId", 'CreditConsumed', NEW."ConsumedAtUtc");
            ELSIF NEW."State" = 'Released' THEN
                PERFORM hireyar_usage_event(NEW."OrganizationId", NEW."InterviewSessionId", 'Released', NEW."ReleasedAtUtc");
            END IF;
            RETURN NEW;
        END; $fn$;
        CREATE TRIGGER interview_usage_quota AFTER INSERT OR UPDATE ON "InterviewQuotaReservations"
            FOR EACH ROW EXECUTE FUNCTION hireyar_usage_quota_event();

        CREATE FUNCTION hireyar_record_usage_expirations(at_utc timestamptz) RETURNS integer LANGUAGE plpgsql AS $fn$
        DECLARE s record; inserted integer := 0;
        BEGIN
            FOR s IN SELECT i."Id", i."OrganizationId", i."ExpiresAtUtc" FROM "InterviewSessions" i
                WHERE i."ExpiresAtUtc" <= at_utc AND i."Status" NOT IN ('Completed','Cancelled')
                AND i."AccessRevokedAtUtc" IS NULL AND i."DeletedAtUtc" IS NULL
                AND NOT EXISTS (SELECT 1 FROM "InterviewUsageEvents" e WHERE e."InterviewSessionId" = i."Id" AND e."EventType" = 'Expired')
                ORDER BY i."ExpiresAtUtc", i."Id" LIMIT 200 FOR UPDATE OF i SKIP LOCKED
            LOOP
                PERFORM hireyar_usage_event(s."OrganizationId", s."Id", 'Expired', s."ExpiresAtUtc");
                inserted := inserted + 1;
            END LOOP;
            RETURN inserted;
        END; $fn$;

        -- Only facts recoverable from existing rows; never fabricate missing transitions or charges.
        INSERT INTO "InterviewUsageEvents" ("Id", "OrganizationId", "InterviewSessionId", "EventType", "OccurredAtUtc", "IsHistorical")
        SELECT gen_random_uuid(), s."OrganizationId", s."Id", v.kind, v.occurred, true
        FROM "InterviewSessions" s LEFT JOIN "InterviewQuotaReservations" r
            ON r."InterviewSessionId" = s."Id" AND r."OrganizationId" = s."OrganizationId"
        CROSS JOIN LATERAL (VALUES
            ('Created', s."CreatedAtUtc"), ('Started', s."StartedAtUtc"),
            ('Completed', CASE WHEN s."Status" = 'Completed' THEN s."CompletedAtUtc" END),
            ('Cancelled', CASE WHEN s."Status" = 'Cancelled' THEN s."CompletedAtUtc" END),
            ('Replaced', CASE WHEN s."SupersededByInterviewId" IS NOT NULL THEN s."AccessRevokedAtUtc" END),
            ('Deleted', s."DeletedAtUtc"),
            ('AccessRevoked', CASE WHEN s."SupersededByInterviewId" IS NULL AND s."DeletedAtUtc" IS NULL AND s."Status" <> 'Cancelled' THEN s."AccessRevokedAtUtc" END),
            ('Reserved', r."CreatedAtUtc"), ('CreditConsumed', r."ConsumedAtUtc"), ('Released', r."ReleasedAtUtc"),
            ('Expired', CASE WHEN s."ExpiresAtUtc" <= transaction_timestamp()
                AND s."ExpiresAtUtc" <= LEAST(s."CompletedAtUtc", s."AccessRevokedAtUtc", s."DeletedAtUtc", 'infinity'::timestamptz)
                THEN s."ExpiresAtUtc" END)
        ) v(kind, occurred) WHERE v.occurred IS NOT NULL;

        CREATE FUNCTION hireyar_usage_immutable() RETURNS trigger LANGUAGE plpgsql AS $fn$
        BEGIN RAISE EXCEPTION 'Interview usage events are immutable'; END; $fn$;
        CREATE TRIGGER interview_usage_immutable BEFORE UPDATE OR DELETE ON "InterviewUsageEvents"
            FOR EACH ROW EXECUTE FUNCTION hireyar_usage_immutable();
        """;

    internal const string Down = """
        DO $fn$ BEGIN
            IF EXISTS (SELECT 1 FROM "InterviewUsageEvents") THEN
                RAISE EXCEPTION 'Cannot remove interview usage history containing events; restore a backup instead';
            END IF;
        END; $fn$;
        DROP TRIGGER interview_usage_session ON "InterviewSessions";
        DROP TRIGGER interview_usage_quota ON "InterviewQuotaReservations";
        DROP FUNCTION hireyar_usage_session_event();
        DROP FUNCTION hireyar_usage_quota_event();
        DROP FUNCTION hireyar_record_usage_expirations(timestamptz);
        DROP FUNCTION hireyar_usage_event(uuid,uuid,text,timestamptz,boolean);
        DROP TRIGGER interview_usage_immutable ON "InterviewUsageEvents";
        DROP FUNCTION hireyar_usage_immutable();
        """;
}
