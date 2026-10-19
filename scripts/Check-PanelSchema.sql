-- Read-only diagnostic for /api/dashboard and interview management.
-- Run against the same database and search_path used by the deployed API.
-- Does not read application/user data or apply migrations.
BEGIN TRANSACTION READ ONLY;

SELECT current_schema() AS schema, current_setting('search_path') AS search_path;

WITH required(table_name, column_name) AS (VALUES
    ('InterviewSessions', 'PositionTitleSnapshot'),
    ('InterviewSessions', 'TemplateTitleSnapshot'),
    ('CandidateApplications', 'ResumeStorageKey'),
    ('InterviewSessions', 'InterviewType')

)
SELECT table_name, column_name,
    CASE WHEN to_regclass(format('%I', table_name)) IS NULL THEN 'MISSING TABLE'
         WHEN NOT EXISTS (
             SELECT 1 FROM pg_attribute
             WHERE attrelid = to_regclass(format('%I', table_name))
               AND attname = column_name AND attnum > 0 AND NOT attisdropped
         ) THEN 'MISSING COLUMN'
         ELSE 'OK' END AS status
FROM required ORDER BY table_name, column_name;

-- EF history must contain the forward-only removal migration.
SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN ('20261019065637_RemoveLegacyPackagingAndBilling')
ORDER BY "MigrationId";

ROLLBACK;
