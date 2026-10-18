using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageSpecializedInterviewPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SpecializedAiPricePerMinuteUsd",
                table: "PricingPackages",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Package_SpecializedPrice",
                table: "PricingPackages",
                sql: "\"SpecializedAiPricePerMinuteUsd\" IS NULL OR (\"SpecializedAiPricePerMinuteUsd\" > 0 AND \"SpecializedAiPricePerMinuteUsd\" <= 1000)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Package_SpecializedPrice",
                table: "PricingPackages");

            migrationBuilder.DropColumn(
                name: "SpecializedAiPricePerMinuteUsd",
                table: "PricingPackages");
        }
    }
}
