using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionCreditPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmountSnapshot",
                table: "SubscriptionPurchases",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "SubscriptionPurchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "purchase");

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmountSnapshot",
                table: "SubscriptionPurchases",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProratedCreditAmountSnapshot",
                table: "SubscriptionPurchases",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesSubscriptionId",
                table: "SubscriptionPurchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalAmountSnapshot",
                table: "Payments",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmountSnapshot",
                table: "Payments",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercentSnapshot",
                table: "Payments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionReplacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionReplacements", x => x.Id);
                    table.UniqueConstraint("AK_SubscriptionReplacements_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_SubscriptionReplacements_OrganizationSubscriptions_Organiza~",
                        columns: x => new { x.OrganizationId, x.SubscriptionId },
                        principalTable: "OrganizationSubscriptions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionReplacements_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionReplacements_Payments_OrganizationId_PaymentId",
                        columns: x => new { x.OrganizationId, x.PaymentId },
                        principalTable: "Payments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_OrganizationId_ReplacesSubscriptionId",
                table: "SubscriptionPurchases",
                columns: new[] { "OrganizationId", "ReplacesSubscriptionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Purchase_Operation",
                table: "SubscriptionPurchases",
                sql: "\"Operation\" IN ('purchase','renewal','upgrade') AND \"ProratedCreditAmountSnapshot\" BETWEEN 0 AND \"PriceAmountSnapshot\" AND ((\"Operation\" = 'upgrade' AND \"ReplacesSubscriptionId\" IS NOT NULL) OR (\"Operation\" <> 'upgrade' AND \"ReplacesSubscriptionId\" IS NULL AND \"ProratedCreditAmountSnapshot\" = 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payment_Tax",
                table: "Payments",
                sql: "(\"SubtotalAmountSnapshot\" IS NULL AND \"TaxAmountSnapshot\" IS NULL AND \"TaxPercentSnapshot\" IS NULL) OR (\"SubtotalAmountSnapshot\" IS NOT NULL AND \"TaxAmountSnapshot\" IS NOT NULL AND \"TaxPercentSnapshot\" IS NOT NULL AND \"SubtotalAmountSnapshot\" > 0 AND \"TaxAmountSnapshot\" >= 0 AND \"TaxPercentSnapshot\" BETWEEN 0 AND 100 AND \"Amount\" = \"SubtotalAmountSnapshot\" + \"TaxAmountSnapshot\")");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReplacements_OrganizationId_CreatedAtUtc_Id",
                table: "SubscriptionReplacements",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReplacements_OrganizationId_PaymentId",
                table: "SubscriptionReplacements",
                columns: new[] { "OrganizationId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReplacements_OrganizationId_SubscriptionId",
                table: "SubscriptionReplacements",
                columns: new[] { "OrganizationId", "SubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReplacements_PaymentId",
                table: "SubscriptionReplacements",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionReplacements_SubscriptionId",
                table: "SubscriptionReplacements",
                column: "SubscriptionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionPurchases_OrganizationSubscriptions_Organizatio~",
                table: "SubscriptionPurchases",
                columns: new[] { "OrganizationId", "ReplacesSubscriptionId" },
                principalTable: "OrganizationSubscriptions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(SubscriptionReplacementGuards.Up);
            migrationBuilder.Sql(SubscriptionReplacementGuards.CatalogUpdate);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SubscriptionReplacementGuards.Down);
            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionPurchases_OrganizationSubscriptions_Organizatio~",
                table: "SubscriptionPurchases");

            migrationBuilder.DropTable(
                name: "SubscriptionReplacements");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPurchases_OrganizationId_ReplacesSubscriptionId",
                table: "SubscriptionPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Purchase_Operation",
                table: "SubscriptionPurchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payment_Tax",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DiscountAmountSnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "OriginalAmountSnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "ProratedCreditAmountSnapshot",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "ReplacesSubscriptionId",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "SubtotalAmountSnapshot",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TaxAmountSnapshot",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TaxPercentSnapshot",
                table: "Payments");
        }
    }
}
