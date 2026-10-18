using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewTypeTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewTariffs",
                columns: table => new
                {
                    InterviewType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RatePerMinuteUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewTariffs", x => x.InterviewType);
                    table.CheckConstraint("CK_InterviewTariffs_Rate", "\"RatePerMinuteUsd\" > 0 AND \"RatePerMinuteUsd\" <= 1000");
                    table.CheckConstraint("CK_InterviewTariffs_Type", "\"InterviewType\" IN ('general', 'specialized')");
                });

            migrationBuilder.InsertData(
                table: "InterviewTariffs",
                columns: new[] { "InterviewType", "RatePerMinuteUsd" },
                values: new object[,]
                {
                    { "general", 0.3m },
                    { "specialized", 0.8m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewTariffs");
        }
    }
}
