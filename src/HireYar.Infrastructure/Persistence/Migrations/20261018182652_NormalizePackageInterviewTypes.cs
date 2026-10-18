using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePackageInterviewTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Purchase_Values",
                table: "SubscriptionPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RegistrationTrial_Values",
                table: "RegistrationTrials");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Package_SpecializedPrice",
                table: "PricingPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Package_Values",
                table: "PricingPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Template_Type",
                table: "InterviewTemplates");

            migrationBuilder.AlterColumn<string>(
                name: "InterviewType",
                table: "InterviewTemplates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "PricingInterviewTypeCode",
                table: "InterviewSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InterviewTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EngineType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewTypes", x => x.Code);
                    table.CheckConstraint("CK_InterviewType_Code", "\"Code\" ~ '^[a-z][a-z0-9_]{1,63}$'");
                    table.CheckConstraint("CK_InterviewType_Engine", "\"EngineType\" IN ('general','specialized')");
                });

            migrationBuilder.CreateTable(
                name: "PackageInterviewTypes",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewTypeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PricePerMinuteUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageInterviewTypes", x => new { x.PackageId, x.InterviewTypeCode });
                    table.CheckConstraint("CK_PackageInterviewType_Price", "\"PricePerMinuteUsd\" > 0 AND \"PricePerMinuteUsd\" <= 1000");
                    table.ForeignKey(
                        name: "FK_PackageInterviewTypes_InterviewTypes_InterviewTypeCode",
                        column: x => x.InterviewTypeCode,
                        principalTable: "InterviewTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageInterviewTypes_PricingPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "PricingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Initial rates explicitly supplied by the user: general $0.03, specialized $0.05.
            // Historical billing and purchase snapshots are not rewritten.
            migrationBuilder.Sql("""
                INSERT INTO "InterviewTypes" ("Code", "Name", "EngineType", "IsActive", "SortOrder", "RowVersion")
                VALUES ('general', 'مصاحبه عمومی', 'general', true, 1, decode(md5('interview-type-general'), 'hex')),
                       ('specialized', 'مصاحبه تخصصی', 'specialized', true, 2, decode(md5('interview-type-specialized'), 'hex'));
                INSERT INTO "PackageInterviewTypes" ("PackageId", "InterviewTypeCode", "PricePerMinuteUsd", "IsActive")
                SELECT p."Id", 'general', 0.03,
                    EXISTS (SELECT 1 FROM "PackageFeatures" pf JOIN "FeatureDefinitions" f ON f."Id" = pf."FeatureId"
                            WHERE pf."PackageId" = p."Id" AND pf."IsEnabled" AND f."Code" = 'hr_interview')
                FROM "PricingPackages" p;
                INSERT INTO "PackageInterviewTypes" ("PackageId", "InterviewTypeCode", "PricePerMinuteUsd", "IsActive")
                SELECT p."Id", 'specialized', 0.05,
                    EXISTS (SELECT 1 FROM "PackageFeatures" pf JOIN "FeatureDefinitions" f ON f."Id" = pf."FeatureId"
                            WHERE pf."PackageId" = p."Id" AND pf."IsEnabled" AND f."Code" = 'technical_interview')
                FROM "PricingPackages" p WHERE p."SpecializedAiPricePerMinuteUsd" IS NOT NULL
                    OR EXISTS (SELECT 1 FROM "PackageFeatures" pf JOIN "FeatureDefinitions" f ON f."Id" = pf."FeatureId"
                               WHERE pf."PackageId" = p."Id" AND pf."IsEnabled" AND f."Code" = 'technical_interview');
                """);

            migrationBuilder.DropColumn(
                name: "AiPricePerMinuteUsd",
                table: "PricingPackages");

            migrationBuilder.DropColumn(
                name: "SpecializedAiPricePerMinuteUsd",
                table: "PricingPackages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Purchase_Values",
                table: "SubscriptionPurchases",
                sql: "\"PeriodMonthsSnapshot\" BETWEEN 1 AND 120 AND \"PriceAmountSnapshot\" > 0 AND \"CurrencySnapshot\" = 'IRT' AND \"AiPricePerMinuteUsdSnapshot\" >= 0 AND \"RetentionDaysSnapshot\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RegistrationTrial_Values",
                table: "RegistrationTrials",
                sql: "\"CreditAmountRial\" > 0 AND \"AiPricePerMinuteUsd\" >= 0 AND \"RetentionDays\" BETWEEN 1 AND 3650");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Package_Values",
                table: "PricingPackages",
                sql: "\"InterviewRetentionDays\" BETWEEN 1 AND 3650");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewTemplates_InterviewType",
                table: "InterviewTemplates",
                column: "InterviewType");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_PricingInterviewTypeCode",
                table: "InterviewSessions",
                column: "PricingInterviewTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_PackageInterviewTypes_InterviewTypeCode",
                table: "PackageInterviewTypes",
                column: "InterviewTypeCode");

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewSessions_InterviewTypes_PricingInterviewTypeCode",
                table: "InterviewSessions",
                column: "PricingInterviewTypeCode",
                principalTable: "InterviewTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewTemplates_InterviewTypes_InterviewType",
                table: "InterviewTemplates",
                column: "InterviewType",
                principalTable: "InterviewTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore only states that the old two-column model can represent without data loss.
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "InterviewTypes" WHERE "Code" NOT IN ('general','specialized')
                        OR NOT "IsActive" OR "EngineType" <> "Code"
                        OR "Name" <> CASE "Code" WHEN 'general' THEN 'مصاحبه عمومی' ELSE 'مصاحبه تخصصی' END
                        OR "SortOrder" <> CASE "Code" WHEN 'general' THEN 1 ELSE 2 END)
                        OR EXISTS (SELECT 1 FROM "PricingPackages" p WHERE NOT EXISTS (
                            SELECT 1 FROM "PackageInterviewTypes" r WHERE r."PackageId" = p."Id" AND r."InterviewTypeCode" = 'general'))
                        OR EXISTS (SELECT 1 FROM "PackageInterviewTypes" r WHERE r."IsActive" <> EXISTS (
                            SELECT 1 FROM "PackageFeatures" pf JOIN "FeatureDefinitions" f ON f."Id" = pf."FeatureId"
                            WHERE pf."PackageId" = r."PackageId" AND pf."IsEnabled"
                                AND f."Code" = CASE r."InterviewTypeCode" WHEN 'general' THEN 'hr_interview' ELSE 'technical_interview' END))
                        OR EXISTS (SELECT 1 FROM "SubscriptionPurchases" WHERE "AiPricePerMinuteUsdSnapshot" = 0)
                        OR EXISTS (SELECT 1 FROM "RegistrationTrials" WHERE "AiPricePerMinuteUsd" = 0)
                    THEN RAISE EXCEPTION 'Dynamic interview types cannot be downgraded without data loss; restore a verified backup.';
                    END IF;
                END $guard$;
                ALTER TABLE "PricingPackages" ADD COLUMN "AiPricePerMinuteUsd" numeric(18,6) NOT NULL DEFAULT 0;
                ALTER TABLE "PricingPackages" ADD COLUMN "SpecializedAiPricePerMinuteUsd" numeric(18,6) NULL;
                UPDATE "PricingPackages" p SET "AiPricePerMinuteUsd" = r."PricePerMinuteUsd"
                    FROM "PackageInterviewTypes" r WHERE r."PackageId" = p."Id" AND r."InterviewTypeCode" = 'general';
                UPDATE "PricingPackages" p SET "SpecializedAiPricePerMinuteUsd" = r."PricePerMinuteUsd"
                    FROM "PackageInterviewTypes" r WHERE r."PackageId" = p."Id" AND r."InterviewTypeCode" = 'specialized';
                ALTER TABLE "PricingPackages" ALTER COLUMN "AiPricePerMinuteUsd" DROP DEFAULT;
                ALTER TABLE "InterviewSessions" DROP CONSTRAINT "FK_InterviewSessions_InterviewTypes_PricingInterviewTypeCode";
                ALTER TABLE "InterviewTemplates" DROP CONSTRAINT "FK_InterviewTemplates_InterviewTypes_InterviewType";
                DROP INDEX "IX_InterviewSessions_PricingInterviewTypeCode";
                DROP INDEX "IX_InterviewTemplates_InterviewType";
                ALTER TABLE "InterviewSessions" DROP COLUMN "PricingInterviewTypeCode";
                ALTER TABLE "InterviewTemplates" ALTER COLUMN "InterviewType" TYPE character varying(20);
                ALTER TABLE "InterviewTemplates" ADD CONSTRAINT "CK_Template_Type" CHECK ("InterviewType" IN ('general','specialized'));
                DROP TABLE "PackageInterviewTypes";
                DROP TABLE "InterviewTypes";
                ALTER TABLE "PricingPackages" DROP CONSTRAINT "CK_Package_Values";
                ALTER TABLE "PricingPackages" ADD CONSTRAINT "CK_Package_Values" CHECK ("AiPricePerMinuteUsd" > 0 AND "InterviewRetentionDays" BETWEEN 1 AND 3650);
                ALTER TABLE "PricingPackages" ADD CONSTRAINT "CK_Package_SpecializedPrice" CHECK ("SpecializedAiPricePerMinuteUsd" IS NULL OR ("SpecializedAiPricePerMinuteUsd" > 0 AND "SpecializedAiPricePerMinuteUsd" <= 1000));
                ALTER TABLE "SubscriptionPurchases" DROP CONSTRAINT "CK_Purchase_Values";
                ALTER TABLE "SubscriptionPurchases" ADD CONSTRAINT "CK_Purchase_Values" CHECK ("PeriodMonthsSnapshot" BETWEEN 1 AND 120 AND "PriceAmountSnapshot" > 0 AND "CurrencySnapshot" = 'IRT' AND "AiPricePerMinuteUsdSnapshot" > 0 AND "RetentionDaysSnapshot" > 0);
                ALTER TABLE "RegistrationTrials" DROP CONSTRAINT "CK_RegistrationTrial_Values";
                ALTER TABLE "RegistrationTrials" ADD CONSTRAINT "CK_RegistrationTrial_Values" CHECK ("CreditAmountRial" > 0 AND "AiPricePerMinuteUsd" > 0 AND "RetentionDays" BETWEEN 1 AND 3650);
                """);
        }
    }
}
