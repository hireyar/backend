using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.UniqueConstraint("AK_SupportTickets_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_SupportTicket_Subject", "length(btrim(\"Subject\")) BETWEEN 1 AND 200");
                    table.CheckConstraint("CK_SupportTicket_Values", "\"Status\" BETWEEN 0 AND 2 AND \"Priority\" BETWEEN 0 AND 2 AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_SupportTickets_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsStaff = table.Column<bool>(type: "boolean", nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                    table.UniqueConstraint("AK_TicketMessages_OrganizationId_TicketId_Id", x => new { x.OrganizationId, x.TicketId, x.Id });
                    table.CheckConstraint("CK_TicketMessage_Body", "length(btrim(\"Body\")) BETWEEN 1 AND 8000");
                    table.ForeignKey(
                        name: "FK_TicketMessages_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketMessages_SupportTickets_OrganizationId_TicketId",
                        columns: x => new { x.OrganizationId, x.TicketId },
                        principalTable: "SupportTickets",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Length = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAttachments", x => x.Id);
                    table.CheckConstraint("CK_TicketAttachment_Length", "\"Length\" BETWEEN 1 AND 5242880");
                    table.CheckConstraint("CK_TicketAttachment_Type", "\"ContentType\" IN ('image/png','image/jpeg','application/pdf','text/plain')");
                    table.ForeignKey(
                        name: "FK_TicketAttachments_TicketMessages_OrganizationId_TicketId_Me~",
                        columns: x => new { x.OrganizationId, x.TicketId, x.MessageId },
                        principalTable: "TicketMessages",
                        principalColumns: new[] { "OrganizationId", "TicketId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CreatedByUserId",
                table: "SupportTickets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_OrganizationId_UpdatedAtUtc_Id",
                table: "SupportTickets",
                columns: new[] { "OrganizationId", "UpdatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status_UpdatedAtUtc_Id",
                table: "SupportTickets",
                columns: new[] { "Status", "UpdatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_OrganizationId_TicketId_MessageId",
                table: "TicketAttachments",
                columns: new[] { "OrganizationId", "TicketId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_StorageKey",
                table: "TicketAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_AuthorUserId",
                table: "TicketMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_OrganizationId_TicketId_CreatedAtUtc_Id",
                table: "TicketMessages",
                columns: new[] { "OrganizationId", "TicketId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "SupportTickets") THEN
                    RAISE EXCEPTION 'Cannot downgrade support ticketing with stored history.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "TicketAttachments");

            migrationBuilder.DropTable(
                name: "TicketMessages");

            migrationBuilder.DropTable(
                name: "SupportTickets");
        }
    }
}
