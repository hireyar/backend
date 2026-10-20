using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mobile = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockoutEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstSalePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RenewalPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAgents", x => x.Id);
                    table.CheckConstraint("CK_SalesAgent_Values", "\"Mobile\" ~ '^09[0-9]{9}$' AND length(btrim(\"FullName\")) > 0 AND \"FirstSalePercent\" BETWEEN 0 AND 100 AND \"RenewalPercent\" BETWEEN 0 AND 100 AND \"FailedAttempts\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SalesLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mobile = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastContactAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FollowUpAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLeads", x => x.Id);
                    table.CheckConstraint("CK_SalesLead_Values", "\"Mobile\" ~ '^09[0-9]{9}$' AND length(btrim(\"CustomerName\")) > 0 AND ((\"OrganizationId\" IS NULL AND \"RegisteredAtUtc\" IS NULL) OR (\"OrganizationId\" IS NOT NULL AND \"RegisteredAtUtc\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_SalesLeads_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeads_SalesAgents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "SalesAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesPayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountRial = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesPayouts", x => x.Id);
                    table.UniqueConstraint("AK_SalesPayouts_AgentId_Id", x => new { x.AgentId, x.Id });
                    table.CheckConstraint("CK_SalesPayout_Values", "\"AmountRial\" > 0 AND ((\"Status\" = 'Requested' AND \"ReviewedAtUtc\" IS NULL AND \"ReviewedById\" IS NULL AND \"ReviewNote\" IS NULL) OR (\"Status\" IN ('Paid','Returned') AND \"ReviewedAtUtc\" IS NOT NULL AND \"ReviewedById\" IS NOT NULL AND length(btrim(\"ReviewNote\")) > 0))");
                    table.ForeignKey(
                        name: "FK_SalesPayouts_SalesAgents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "SalesAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesAudits_SalesLeads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "SalesLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvoiceAmountRial = table.Column<long>(type: "bigint", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AmountRial = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesCommissions", x => x.Id);
                    table.CheckConstraint("CK_SalesCommission_Values", "\"Kind\" IN ('FirstSale','Renewal') AND \"InvoiceAmountRial\" > 0 AND \"Percent\" BETWEEN 0 AND 100 AND \"AmountRial\" = floor(\"InvoiceAmountRial\"::numeric * \"Percent\" / 100) AND \"AvailableAtUtc\" = \"CreatedAtUtc\" + interval '168 hours'");
                    table.ForeignKey(
                        name: "FK_SalesCommissions_SalesAgents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "SalesAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCommissions_SalesLeads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "SalesLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCommissions_SalesPayouts_AgentId_PayoutId",
                        columns: x => new { x.AgentId, x.PayoutId },
                        principalTable: "SalesPayouts",
                        principalColumns: new[] { "AgentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesCommissions_SubscriptionOrders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalTable: "SubscriptionOrders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAgents_Mobile",
                table: "SalesAgents",
                column: "Mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesAudits_LeadId",
                table: "SalesAudits",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissions_AgentId_CreatedAtUtc",
                table: "SalesCommissions",
                columns: new[] { "AgentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissions_AgentId_PayoutId",
                table: "SalesCommissions",
                columns: new[] { "AgentId", "PayoutId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissions_LeadId",
                table: "SalesCommissions",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissions_OrderId",
                table: "SalesCommissions",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesCommissions_OrganizationId_OrderId",
                table: "SalesCommissions",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_AgentId_CreatedAtUtc",
                table: "SalesLeads",
                columns: new[] { "AgentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_Mobile",
                table: "SalesLeads",
                column: "Mobile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_OrganizationId",
                table: "SalesLeads",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayouts_AgentId_RequestId",
                table: "SalesPayouts",
                columns: new[] { "AgentId", "RequestId" },
                unique: true);
            SalesGuards.Up(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            SalesGuards.Down(migrationBuilder);
            migrationBuilder.DropTable(
                name: "SalesAudits");

            migrationBuilder.DropTable(
                name: "SalesCommissions");

            migrationBuilder.DropTable(
                name: "SalesLeads");

            migrationBuilder.DropTable(
                name: "SalesPayouts");

            migrationBuilder.DropTable(
                name: "SalesAgents");
        }
    }
}
