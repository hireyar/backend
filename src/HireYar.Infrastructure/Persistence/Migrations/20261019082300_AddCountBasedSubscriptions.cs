using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCountBasedSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewQuotaReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewQuotaReservations", x => x.Id);
                    table.UniqueConstraint("AK_InterviewQuotaReservations_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_QuotaReservation_Retention", "\"RetentionDays\" BETWEEN 1 AND 3650");
                    table.CheckConstraint("CK_QuotaReservation_State", "(\"State\"='Reserved' AND \"ConsumedAtUtc\" IS NULL AND \"ReleasedAtUtc\" IS NULL) OR (\"State\"='Consumed' AND \"ConsumedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" IS NULL) OR (\"State\"='Released' AND \"ReleasedAtUtc\" IS NOT NULL AND \"ConsumedAtUtc\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_InterviewQuotaReservations_InterviewSessions_OrganizationId~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewQuotaReservations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Product = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BundleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: false),
                    TeamUsers = table.Column<int>(type: "integer", nullable: false),
                    UnlimitedTeam = table.Column<bool>(type: "boolean", nullable: false),
                    UnitPriceRial = table.Column<long>(type: "bigint", nullable: false),
                    TeamMonthlyPriceRial = table.Column<long>(type: "bigint", nullable: false),
                    OriginalAmountRial = table.Column<long>(type: "bigint", nullable: false),
                    AmountRial = table.Column<long>(type: "bigint", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionOrders", x => x.Id);
                    table.UniqueConstraint("AK_SubscriptionOrders_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_SubscriptionOrder_Confirmation", "(\"Status\"='Confirmed' AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"PaymentReference\" IS NOT NULL AND length(btrim(\"PaymentReference\")) > 0) OR (\"Status\" <> 'Confirmed' AND \"ConfirmedAtUtc\" IS NULL AND \"ConfirmedByUserId\" IS NULL AND \"PaymentReference\" IS NULL)");
                    table.CheckConstraint("CK_SubscriptionOrder_Product", "(\"Product\"='subscription' AND \"Months\" IN (1,3,6,12) AND \"TeamUsers\" >= 1 AND \"Quantity\"=0 AND \"BundleCode\" IS NULL) OR (\"Product\"='interviews' AND \"Quantity\" > 0 AND \"Months\"=0 AND \"TeamUsers\"=0 AND \"BundleCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_SubscriptionOrder_Values", "\"AmountRial\" > 0 AND \"OriginalAmountRial\" >= \"AmountRial\" AND \"ExpiresAtUtc\" > \"CreatedAtUtc\" AND \"Status\" IN ('Pending','Confirmed','Cancelled') AND \"Product\" IN ('subscription','interviews')");
                    table.ForeignKey(
                        name: "FK_SubscriptionOrders_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MonthlyPriceRial = table.Column<long>(type: "bigint", nullable: false),
                    BaseInterviewPriceRial = table.Column<long>(type: "bigint", nullable: false),
                    ExtraUserMonthlyPriceRial = table.Column<long>(type: "bigint", nullable: false),
                    IncludedUsers = table.Column<int>(type: "integer", nullable: false),
                    PaidUserCeiling = table.Column<int>(type: "integer", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionPlan_Singleton", "\"Id\" = 'd1000000-0000-0000-0000-000000000001'::uuid");
                    table.CheckConstraint("CK_SubscriptionPlan_Values", "\"MonthlyPriceRial\" BETWEEN 1 AND 1000000000000 AND \"BaseInterviewPriceRial\" BETWEEN 1 AND 1000000000000 AND \"ExtraUserMonthlyPriceRial\" BETWEEN 0 AND 1000000000000 AND \"IncludedUsers\" >= 1 AND \"PaidUserCeiling\" > \"IncludedUsers\" AND \"RetentionDays\" BETWEEN 1 AND 3650");
                });

            migrationBuilder.CreateTable(
                name: "InterviewQuotaGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewQuotaGrants", x => x.Id);
                    table.UniqueConstraint("AK_InterviewQuotaGrants_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_QuotaGrant_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InterviewQuotaGrants_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewQuotaGrants_SubscriptionOrders_OrganizationId_Orde~",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalTable: "SubscriptionOrders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TeamUsers = table.Column<int>(type: "integer", nullable: false),
                    UnlimitedTeam = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMemberships", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationMemberships_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Membership_Values", "\"ExpiresAtUtc\" > \"StartedAtUtc\" AND \"TeamUsers\" > 0 AND \"RetentionDays\" BETWEEN 1 AND 3650");
                    table.ForeignKey(
                        name: "FK_OrganizationMemberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationMemberships_SubscriptionOrders_OrganizationId_O~",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalTable: "SubscriptionOrders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaximumQuantity = table.Column<int>(type: "integer", nullable: false),
                    QuantityStep = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewBundles", x => x.Id);
                    table.CheckConstraint("CK_InterviewBundle_Values", "\"MinimumQuantity\" BETWEEN 1 AND 1000000 AND \"MaximumQuantity\" BETWEEN \"MinimumQuantity\" AND 1000000 AND \"QuantityStep\" >= 1 AND (\"MaximumQuantity\" - \"MinimumQuantity\") % \"QuantityStep\" = 0 AND \"DiscountPercent\" BETWEEN 0 AND 99.99");
                    table.ForeignKey(
                        name: "FK_InterviewBundles_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "BaseInterviewPriceRial", "ExtraUserMonthlyPriceRial", "IncludedUsers", "IsActive", "MonthlyPriceRial", "Name", "PaidUserCeiling", "RetentionDays", "RowVersion" },
                values: new object[] { new Guid("d1000000-0000-0000-0000-000000000001"), 4000000L, 2000000L, 5, true, 90000000L, "اشتراک هایریار", 30, 60, new byte[] { 0, 0, 0, 209, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 } });

            migrationBuilder.InsertData(
                table: "InterviewBundles",
                columns: new[] { "Id", "Code", "DiscountPercent", "IsActive", "MaximumQuantity", "MinimumQuantity", "Name", "PlanId", "QuantityStep", "RowVersion", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("d2000000-0000-0000-0000-000000000001"), "dana", 0m, true, 10, 10, "دانا", new Guid("d1000000-0000-0000-0000-000000000001"), 1, new byte[] { 0, 0, 0, 211, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }, 1 },
                    { new Guid("d2000000-0000-0000-0000-000000000002"), "bina", 6.25m, true, 20, 20, "بینا", new Guid("d1000000-0000-0000-0000-000000000001"), 1, new byte[] { 0, 0, 0, 211, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 }, 2 },
                    { new Guid("d2000000-0000-0000-0000-000000000003"), "zharfa", 5m, true, 50, 50, "ژرفا", new Guid("d1000000-0000-0000-0000-000000000001"), 1, new byte[] { 0, 0, 0, 211, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3 }, 3 },
                    { new Guid("d2000000-0000-0000-0000-000000000004"), "custom", 0m, true, 300, 50, "دلخواه", new Guid("d1000000-0000-0000-0000-000000000001"), 10, new byte[] { 0, 0, 0, 211, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4 }, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewBundles_Code",
                table: "InterviewBundles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewBundles_PlanId",
                table: "InterviewBundles",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuotaGrants_OrderId",
                table: "InterviewQuotaGrants",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuotaGrants_OrganizationId_OrderId",
                table: "InterviewQuotaGrants",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuotaReservations_InterviewSessionId",
                table: "InterviewQuotaReservations",
                column: "InterviewSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuotaReservations_OrganizationId_InterviewSessionId",
                table: "InterviewQuotaReservations",
                columns: new[] { "OrganizationId", "InterviewSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuotaReservations_OrganizationId_State",
                table: "InterviewQuotaReservations",
                columns: new[] { "OrganizationId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrderId",
                table: "OrganizationMemberships",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_OrderId",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_StartedAtUtc_Expires~",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "StartedAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionOrders_OrganizationId_CreatedAtUtc",
                table: "SubscriptionOrders",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionOrders_OrganizationId_RequestId",
                table: "SubscriptionOrders",
                columns: new[] { "OrganizationId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionOrders_PaymentReference",
                table: "SubscriptionOrders",
                column: "PaymentReference",
                unique: true,
                filter: "\"PaymentReference\" IS NOT NULL");
            migrationBuilder.Sql("""
                CREATE FUNCTION hy_commerce_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                    RAISE EXCEPTION 'Confirmed membership and quota grants are immutable';
                END $$;
                CREATE TRIGGER membership_immutable BEFORE UPDATE OR DELETE ON "OrganizationMemberships"
                    FOR EACH ROW EXECUTE FUNCTION hy_commerce_immutable();
                CREATE TRIGGER quota_grant_immutable BEFORE UPDATE OR DELETE ON "InterviewQuotaGrants"
                    FOR EACH ROW EXECUTE FUNCTION hy_commerce_immutable();
                CREATE FUNCTION hy_commerce_order_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                    IF TG_OP = 'DELETE' OR OLD."Status" <> 'Pending' OR NEW."Status" NOT IN ('Confirmed','Cancelled') OR
                        (to_jsonb(NEW) - ARRAY['Status','ConfirmedAtUtc','ConfirmedByUserId','PaymentReference']) <>
                        (to_jsonb(OLD) - ARRAY['Status','ConfirmedAtUtc','ConfirmedByUserId','PaymentReference']) THEN
                        RAISE EXCEPTION 'Order snapshots and finalized orders are immutable';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER commerce_order_guard BEFORE UPDATE OR DELETE ON "SubscriptionOrders"
                    FOR EACH ROW EXECUTE FUNCTION hy_commerce_order_guard();
                CREATE FUNCTION hy_quota_reservation_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                    IF TG_OP = 'DELETE' OR OLD."State" <> 'Reserved' OR NEW."State" NOT IN ('Consumed','Released') OR
                        (to_jsonb(NEW) - ARRAY['State','ConsumedAtUtc','ReleasedAtUtc']) <>
                        (to_jsonb(OLD) - ARRAY['State','ConsumedAtUtc','ReleasedAtUtc']) THEN
                        RAISE EXCEPTION 'Final quota consumption and reservation snapshots are immutable';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER quota_reservation_guard BEFORE UPDATE OR DELETE ON "InterviewQuotaReservations"
                    FOR EACH ROW EXECUTE FUNCTION hy_quota_reservation_guard();
                CREATE FUNCTION hy_commerce_fulfillment() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE target uuid; purchase "SubscriptionOrders"%ROWTYPE; BEGIN
                    IF TG_TABLE_NAME = 'SubscriptionOrders' THEN target := NEW."Id"; ELSE target := NEW."OrderId"; END IF;
                    SELECT * INTO purchase FROM "SubscriptionOrders" WHERE "Id" = target;
                    IF purchase."Status" = 'Confirmed' AND purchase."Product" = 'subscription' THEN
                        IF NOT EXISTS (SELECT 1 FROM "OrganizationMemberships" m WHERE m."OrderId" = target AND
                            m."OrganizationId" = purchase."OrganizationId" AND m."TeamUsers" = purchase."TeamUsers" AND
                            m."UnlimitedTeam" = purchase."UnlimitedTeam" AND m."RetentionDays" = purchase."RetentionDays" AND
                            m."ExpiresAtUtc" = ((m."StartedAtUtc" AT TIME ZONE 'UTC') + make_interval(months => purchase."Months")) AT TIME ZONE 'UTC') OR
                            EXISTS (SELECT 1 FROM "InterviewQuotaGrants" g WHERE g."OrderId" = target) THEN
                            RAISE EXCEPTION 'A confirmed subscription must grant exactly its purchased membership';
                        END IF;
                    ELSIF purchase."Status" = 'Confirmed' AND purchase."Product" = 'interviews' THEN
                        IF NOT EXISTS (SELECT 1 FROM "InterviewQuotaGrants" g WHERE g."OrderId" = target AND
                            g."OrganizationId" = purchase."OrganizationId" AND g."Quantity" = purchase."Quantity") OR
                            EXISTS (SELECT 1 FROM "OrganizationMemberships" m WHERE m."OrderId" = target) THEN
                            RAISE EXCEPTION 'A confirmed bundle must grant exactly its purchased quantity';
                        END IF;
                    ELSIF EXISTS (SELECT 1 FROM "InterviewQuotaGrants" g WHERE g."OrderId" = target) OR
                          EXISTS (SELECT 1 FROM "OrganizationMemberships" m WHERE m."OrderId" = target) THEN
                        RAISE EXCEPTION 'Unconfirmed orders cannot grant membership or quota';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER commerce_order_fulfilled AFTER INSERT OR UPDATE ON "SubscriptionOrders"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hy_commerce_fulfillment();
                CREATE CONSTRAINT TRIGGER membership_fulfilled AFTER INSERT ON "OrganizationMemberships"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hy_commerce_fulfillment();
                CREATE CONSTRAINT TRIGGER quota_grant_fulfilled AFTER INSERT ON "InterviewQuotaGrants"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hy_commerce_fulfillment();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "SubscriptionOrders") OR EXISTS (SELECT 1 FROM "OrganizationMemberships") OR
                       EXISTS (SELECT 1 FROM "InterviewQuotaGrants") OR EXISTS (SELECT 1 FROM "InterviewQuotaReservations") THEN
                        RAISE EXCEPTION 'Restore a pre-removal backup before discarding subscription or quota history';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "InterviewBundles");

            migrationBuilder.DropTable(
                name: "InterviewQuotaGrants");

            migrationBuilder.DropTable(
                name: "InterviewQuotaReservations");

            migrationBuilder.DropTable(
                name: "OrganizationMemberships");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "SubscriptionOrders");

            migrationBuilder.Sql("""
                DROP FUNCTION hy_commerce_fulfillment();
                DROP FUNCTION hy_quota_reservation_guard();
                DROP FUNCTION hy_commerce_order_guard();
                DROP FUNCTION hy_commerce_immutable();
                """);

        }
    }
}
