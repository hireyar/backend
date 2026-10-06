-- Read-only diagnostic for /api/dashboard and /api/billing/panel.
-- Run against the same database and search_path used by the deployed API.
-- Does not read application/user data or apply migrations.
BEGIN TRANSACTION READ ONLY;

SELECT current_schema() AS schema, current_setting('search_path') AS search_path;

WITH required(table_name, column_name) AS (VALUES
    ('InterviewSessions', 'PositionTitleSnapshot'),
    ('InterviewSessions', 'TemplateTitleSnapshot'),
    ('CandidateApplications', 'ResumeStorageKey'),
    ('SubscriptionReplacements', 'SubscriptionId'),
    ('SubscriptionReplacements', 'EndedAtUtc'),
    ('SubscriptionPurchases', 'Operation'),
    ('SubscriptionPurchases', 'OriginalAmountSnapshot'),
    ('SubscriptionPurchases', 'DiscountAmountSnapshot'),
    ('SubscriptionPurchases', 'ProratedCreditAmountSnapshot'),
    ('SubscriptionPurchases', 'ReplacesSubscriptionId'),
    ('Payments', 'SubtotalAmountSnapshot'),
    ('Payments', 'TaxAmountSnapshot'),
    ('Payments', 'TaxPercentSnapshot')
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

-- EF history should contain both entries; a missing history table is itself diagnostic.
SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" IN ('20261005130515_AddPanelWizard', '20261005133142_AddSubscriptionCreditPanel')
ORDER BY "MigrationId";

ROLLBACK;
