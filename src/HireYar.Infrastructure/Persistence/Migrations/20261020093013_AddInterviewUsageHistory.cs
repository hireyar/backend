using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewUsageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewUsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsHistorical = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewUsageEvents", x => x.Id);
                    table.CheckConstraint("CK_InterviewUsageEvent_Type", "\"EventType\" IN ('Created','Started','Completed','Cancelled','Expired','Replaced','AccessRevoked','Deleted','Reserved','CreditConsumed','Released')");
                    table.ForeignKey(
                        name: "FK_InterviewUsageEvents_InterviewSessions_OrganizationId_Inter~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewUsageEvents_InterviewSessionId_EventType",
                table: "InterviewUsageEvents",
                columns: new[] { "InterviewSessionId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewUsageEvents_OrganizationId_InterviewSessionId",
                table: "InterviewUsageEvents",
                columns: new[] { "OrganizationId", "InterviewSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewUsageEvents_OrganizationId_OccurredAtUtc_Id",
                table: "InterviewUsageEvents",
                columns: new[] { "OrganizationId", "OccurredAtUtc", "Id" });
            migrationBuilder.Sql(InterviewUsageHistorySql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InterviewUsageHistorySql.Down);
            migrationBuilder.DropTable(
                name: "InterviewUsageEvents");
        }
    }
}
