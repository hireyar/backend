using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "SalesLeads",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "SalesLeads",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactOrganizationName",
                table: "SalesLeads",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CustomerAdministrationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerKind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreditQuantity = table.Column<int>(type: "integer", nullable: false),
                    BundleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAdministrationEvents", x => x.Id);
                    table.CheckConstraint("CK_CustomerAdminEvent_Values", "\"CustomerKind\" IN ('User','Lead') AND \"RequestId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(\"Reason\")) > 0 AND ((\"Action\"='InterviewCreditGranted' AND \"CreditQuantity\" BETWEEN 1 AND 100000 AND \"OrganizationId\" IS NOT NULL AND \"BundleCode\" IS NOT NULL) OR (\"Action\" IN ('ProfileUpdated','SellerChanged','SubscriptionSet','SubscriptionDaysAdjusted','SubscriptionStopped') AND \"CreditQuantity\"=0 AND \"BundleCode\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_CustomerAdministrationEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdministrationEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerSubscriptionOverrides",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExcludedOrderIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsStopped = table.Column<bool>(type: "boolean", nullable: false),
                    TeamUsers = table.Column<int>(type: "integer", nullable: false),
                    UnlimitedTeam = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSubscriptionOverrides", x => x.OrganizationId);
                    table.CheckConstraint("CK_CustomerOverride_Values", "(\"IsStopped\" OR \"ExpiresAtUtc\" > \"StartedAtUtc\") AND \"TeamUsers\" > 0 AND \"RetentionDays\" BETWEEN 1 AND 3650");
                    table.ForeignKey(
                        name: "FK_CustomerSubscriptionOverrides_CustomerAdministrationEvents_~",
                        column: x => x.EventId,
                        principalTable: "CustomerAdministrationEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerSubscriptionOverrides_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdministrationEvents_ActorId",
                table: "CustomerAdministrationEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdministrationEvents_CustomerId_RequestId",
                table: "CustomerAdministrationEvents",
                columns: new[] { "CustomerId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdministrationEvents_OrganizationId_CreatedAtUtc",
                table: "CustomerAdministrationEvents",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptionOverrides_EventId",
                table: "CustomerSubscriptionOverrides",
                column: "EventId",
                unique: true);
            CustomerAdministrationGuards.Up(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            CustomerAdministrationGuards.Down(migrationBuilder);
            migrationBuilder.DropTable(
                name: "CustomerSubscriptionOverrides");

            migrationBuilder.DropTable(
                name: "CustomerAdministrationEvents");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "SalesLeads");

            migrationBuilder.DropColumn(
                name: "ContactOrganizationName",
                table: "SalesLeads");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "SalesLeads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
