using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPeriodDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SixMonthDiscountPercent",
                table: "SubscriptionPlans",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ThreeMonthDiscountPercent",
                table: "SubscriptionPlans",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TwelveMonthDiscountPercent",
                table: "SubscriptionPlans",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: new Guid("d1000000-0000-0000-0000-000000000001"),
                columns: new[] { "SixMonthDiscountPercent", "ThreeMonthDiscountPercent", "TwelveMonthDiscountPercent" },
                values: new object[] { 10m, 5m, 15m });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionPlan_PeriodDiscounts",
                table: "SubscriptionPlans",
                sql: "\"ThreeMonthDiscountPercent\" BETWEEN 0 AND 99.99 AND \"SixMonthDiscountPercent\" BETWEEN 0 AND 99.99 AND \"TwelveMonthDiscountPercent\" BETWEEN 0 AND 99.99");
            // Catalog quotes issued before this pricing change must be refreshed.
            migrationBuilder.Sql("UPDATE \"SubscriptionPlans\" SET \"RowVersion\" = uuid_send(gen_random_uuid());");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionPlan_PeriodDiscounts",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "SixMonthDiscountPercent",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "ThreeMonthDiscountPercent",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "TwelveMonthDiscountPercent",
                table: "SubscriptionPlans");
        }
    }
}
