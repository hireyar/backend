using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuestionReadyAtUtc",
                table: "InterviewSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredCapabilityCode",
                table: "InterviewSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "technical_interview");

            migrationBuilder.AddColumn<int>(
                name: "BillableSeconds",
                table: "InterviewAnswers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemAdmin",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiCreditPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreditAmountUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SellPriceAmount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    SellPriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCreditPackages", x => x.Id);
                    table.CheckConstraint("CK_CreditProduct_Values", "\"CreditAmountUsd\" > 0 AND \"SellPriceAmount\" > 0 AND \"SellPriceCurrency\" = 'IRT'");
                });

            migrationBuilder.CreateTable(
                name: "FeatureDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureDefinitions", x => x.Id);
                    table.CheckConstraint("CK_Feature_Type", "\"DataType\" IN ('Boolean','Integer','Text') AND \"Code\" <> 'interview_retention_days'");
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAiWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAiWallets", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationAiWallets_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Wallet_Values", "\"Balance\" >= 0 AND \"Currency\" = 'USD'");
                    table.ForeignKey(
                        name: "FK_OrganizationAiWallets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AiPricePerMinuteUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    InterviewRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPackages", x => x.Id);
                    table.CheckConstraint("CK_Package_Values", "\"AiPricePerMinuteUsd\" > 0 AND \"InterviewRetentionDays\" BETWEEN 1 AND 3650");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderAuthority = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreditPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditPackageNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreditAmountUsdSnapshot = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    EffectiveUsdToIrtSnapshot = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.UniqueConstraint("AK_Payments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Payment_Values", "\"Amount\" > 0 AND \"Currency\" = 'IRT' AND \"Status\" IN ('Pending','Paid','Failed','Cancelled','Expired') AND \"Type\" IN ('Subscription','AiCredit') AND (\"Type\" <> 'AiCredit' OR (\"CreditPackageId\" IS NOT NULL AND \"CreditAmountUsdSnapshot\" > 0 AND \"EffectiveUsdToIrtSnapshot\" > 0)) AND (\"Status\" <> 'Paid' OR (\"PaidAtUtc\" IS NOT NULL AND \"ProviderTransactionId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_Payments_AiCreditPackages_CreditPackageId",
                        column: x => x.CreditPackageId,
                        principalTable: "AiCreditPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiCreditReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservedAmountUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ConsumedAmountUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCreditReservations", x => x.Id);
                    table.UniqueConstraint("AK_AiCreditReservations_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Reservation_Values", "\"ReservedAmountUsd\" >= 0 AND \"ConsumedAmountUsd\" BETWEEN 0 AND \"ReservedAmountUsd\" AND \"Status\" IN ('Active','Consumed','Released','Expired')");
                    table.ForeignKey(
                        name: "FK_AiCreditReservations_InterviewSessions_OrganizationId_Inter~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiCreditReservations_OrganizationAiWallets_OrganizationId_W~",
                        columns: x => new { x.OrganizationId, x.WalletId },
                        principalTable: "OrganizationAiWallets",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiCreditReservations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiCreditTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCreditTransactions", x => x.Id);
                    table.UniqueConstraint("AK_AiCreditTransactions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Ledger_Values", "\"BalanceBefore\" >= 0 AND \"BalanceAfter\" >= 0 AND \"BalanceAfter\" = \"BalanceBefore\" + \"Amount\" AND \"Type\" IN ('CreditPurchase','InterviewUsage','Refund','AdminAdjustment','PromotionalCredit','Correction')");
                    table.ForeignKey(
                        name: "FK_AiCreditTransactions_OrganizationAiWallets_OrganizationId_W~",
                        columns: x => new { x.OrganizationId, x.WalletId },
                        principalTable: "OrganizationAiWallets",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiCreditTransactions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageFeatures",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageFeatures", x => new { x.PackageId, x.FeatureId });
                    table.ForeignKey(
                        name: "FK_PackageFeatures_FeatureDefinitions_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "FeatureDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageFeatures_PricingPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "PricingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackagePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: false),
                    PriceAmount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePeriods", x => x.Id);
                    table.UniqueConstraint("AK_PackagePeriods_PackageId_Id", x => new { x.PackageId, x.Id });
                    table.CheckConstraint("CK_Period_Values", "\"Months\" BETWEEN 1 AND 120 AND \"PriceAmount\" > 0 AND \"Currency\" = 'IRT' AND \"DiscountPercent\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_PackagePeriods_PricingPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "PricingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackagePeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSubscriptions", x => x.Id);
                    table.UniqueConstraint("AK_OrganizationSubscriptions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Subscription_Values", "\"ExpiresAtUtc\" > \"StartedAtUtc\" AND \"Status\" IN ('Scheduled','Active','Expired','Cancelled','Suspended')");
                    table.ForeignKey(
                        name: "FK_OrganizationSubscriptions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationSubscriptions_PackagePeriods_PackageId_PackageP~",
                        columns: x => new { x.PackageId, x.PackagePeriodId },
                        principalTable: "PackagePeriods",
                        principalColumns: new[] { "PackageId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationSubscriptions_Payments_OrganizationId_PaymentId",
                        columns: x => new { x.OrganizationId, x.PaymentId },
                        principalTable: "Payments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackagePeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageCodeSnapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PackageNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PeriodMonthsSnapshot = table.Column<int>(type: "integer", nullable: false),
                    PriceAmountSnapshot = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    CurrencySnapshot = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DiscountPercentSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AiPricePerMinuteUsdSnapshot = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RetentionDaysSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPurchases", x => x.Id);
                    table.UniqueConstraint("AK_SubscriptionPurchases_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Purchase_Values", "\"PeriodMonthsSnapshot\" BETWEEN 1 AND 120 AND \"PriceAmountSnapshot\" > 0 AND \"CurrencySnapshot\" = 'IRT' AND \"AiPricePerMinuteUsdSnapshot\" > 0 AND \"RetentionDaysSnapshot\" > 0");
                    table.ForeignKey(
                        name: "FK_SubscriptionPurchases_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPurchases_PackagePeriods_PackageId_PackagePerio~",
                        columns: x => new { x.PackageId, x.PackagePeriodId },
                        principalTable: "PackagePeriods",
                        principalColumns: new[] { "PackageId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPurchases_Payments_OrganizationId_PaymentId",
                        columns: x => new { x.OrganizationId, x.PaymentId },
                        principalTable: "Payments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewBillings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePerMinuteUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    BillableSeconds = table.Column<int>(type: "integer", nullable: false),
                    BilledAmountUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewBillings", x => x.Id);
                    table.UniqueConstraint("AK_InterviewBillings_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_InterviewBilling_Values", "\"RatePerMinuteUsd\" > 0 AND \"BillableSeconds\" BETWEEN 0 AND 3600 AND \"BilledAmountUsd\" >= 0 AND \"Status\" IN ('Reserved','Settled','Released')");
                    table.ForeignKey(
                        name: "FK_InterviewBillings_InterviewSessions_OrganizationId_Intervie~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewBillings_OrganizationSubscriptions_OrganizationId_~",
                        columns: x => new { x.OrganizationId, x.SubscriptionId },
                        principalTable: "OrganizationSubscriptions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewBillings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionEntitlement",
                columns: table => new
                {
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionEntitlement", x => new { x.PurchaseId, x.Code });
                    table.ForeignKey(
                        name: "FK_SubscriptionEntitlement_SubscriptionPurchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "SubscriptionPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Interview_Capability",
                table: "InterviewSessions",
                sql: "\"RequiredCapabilityCode\" IN ('hr_interview','technical_interview')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Answer_BillableSeconds",
                table: "InterviewAnswers",
                sql: "\"BillableSeconds\" BETWEEN 0 AND 120");

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservations_InterviewSessionId",
                table: "AiCreditReservations",
                column: "InterviewSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservations_OrganizationId_CreatedAtUtc_Id",
                table: "AiCreditReservations",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservations_OrganizationId_InterviewSessionId",
                table: "AiCreditReservations",
                columns: new[] { "OrganizationId", "InterviewSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservations_OrganizationId_WalletId",
                table: "AiCreditReservations",
                columns: new[] { "OrganizationId", "WalletId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditTransactions_OrganizationId_CreatedAtUtc_Id",
                table: "AiCreditTransactions",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditTransactions_OrganizationId_ReferenceType_Reference~",
                table: "AiCreditTransactions",
                columns: new[] { "OrganizationId", "ReferenceType", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditTransactions_OrganizationId_WalletId",
                table: "AiCreditTransactions",
                columns: new[] { "OrganizationId", "WalletId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureDefinitions_Code",
                table: "FeatureDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewBillings_InterviewSessionId",
                table: "InterviewBillings",
                column: "InterviewSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewBillings_OrganizationId_CreatedAtUtc_Id",
                table: "InterviewBillings",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewBillings_OrganizationId_InterviewSessionId",
                table: "InterviewBillings",
                columns: new[] { "OrganizationId", "InterviewSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewBillings_OrganizationId_SubscriptionId",
                table: "InterviewBillings",
                columns: new[] { "OrganizationId", "SubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAiWallets_OrganizationId",
                table: "OrganizationAiWallets",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAiWallets_OrganizationId_CreatedAtUtc_Id",
                table: "OrganizationAiWallets",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_OrganizationId_CreatedAtUtc_Id",
                table: "OrganizationSubscriptions",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_OrganizationId_PaymentId",
                table: "OrganizationSubscriptions",
                columns: new[] { "OrganizationId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_OrganizationId_StartedAtUtc_Expir~",
                table: "OrganizationSubscriptions",
                columns: new[] { "OrganizationId", "StartedAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_PackageId_PackagePeriodId",
                table: "OrganizationSubscriptions",
                columns: new[] { "PackageId", "PackagePeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSubscriptions_PaymentId",
                table: "OrganizationSubscriptions",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageFeatures_FeatureId",
                table: "PackageFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePeriods_PackageId_Months",
                table: "PackagePeriods",
                columns: new[] { "PackageId", "Months" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreditPackageId",
                table: "Payments",
                column: "CreditPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_CreatedAtUtc_Id",
                table: "Payments",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderAuthority",
                table: "Payments",
                columns: new[] { "Provider", "ProviderAuthority" },
                unique: true,
                filter: "\"ProviderAuthority\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderTransactionId",
                table: "Payments",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true,
                filter: "\"ProviderTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPackages_Code",
                table: "PricingPackages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_OrganizationId_CreatedAtUtc_Id",
                table: "SubscriptionPurchases",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_OrganizationId_PaymentId",
                table: "SubscriptionPurchases",
                columns: new[] { "OrganizationId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_PackageId_PackagePeriodId",
                table: "SubscriptionPurchases",
                columns: new[] { "PackageId", "PackagePeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_PaymentId",
                table: "SubscriptionPurchases",
                column: "PaymentId",
                unique: true);
            migrationBuilder.Sql(BillingSeed.Sql);
            migrationBuilder.Sql(BillingSeed.Guards);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION hireyar_billing_immutable() CASCADE; DROP FUNCTION hireyar_subscription_no_overlap() CASCADE; DROP FUNCTION hireyar_payment_guard() CASCADE; DROP FUNCTION hireyar_interview_billing_guard() CASCADE; DROP FUNCTION hireyar_reservation_guard() CASCADE; DROP FUNCTION hireyar_wallet_matches_ledger() CASCADE;");
            migrationBuilder.DropTable(
                name: "AiCreditReservations");

            migrationBuilder.DropTable(
                name: "AiCreditTransactions");

            migrationBuilder.DropTable(
                name: "InterviewBillings");

            migrationBuilder.DropTable(
                name: "PackageFeatures");

            migrationBuilder.DropTable(
                name: "SubscriptionEntitlement");

            migrationBuilder.DropTable(
                name: "OrganizationAiWallets");

            migrationBuilder.DropTable(
                name: "OrganizationSubscriptions");

            migrationBuilder.DropTable(
                name: "FeatureDefinitions");

            migrationBuilder.DropTable(
                name: "SubscriptionPurchases");

            migrationBuilder.DropTable(
                name: "PackagePeriods");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PricingPackages");

            migrationBuilder.DropTable(
                name: "AiCreditPackages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Interview_Capability",
                table: "InterviewSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Answer_BillableSeconds",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "QuestionReadyAtUtc",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "RequiredCapabilityCode",
                table: "InterviewSessions");

            migrationBuilder.DropColumn(
                name: "BillableSeconds",
                table: "InterviewAnswers");

            migrationBuilder.DropColumn(
                name: "IsSystemAdmin",
                table: "AspNetUsers");
        }
    }
}

