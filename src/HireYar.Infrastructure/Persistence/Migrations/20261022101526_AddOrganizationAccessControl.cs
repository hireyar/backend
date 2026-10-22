using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessRoleCode",
                table: "MobileTeamInvitations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "interview_manager");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "MobileTeamInvitations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "MobileTeamInvitations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "fa");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "MobileTeamInvitations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessRoleCode",
                table: "AspNetUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessAudits_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessAudits_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessRoles",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsAdministrator = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRoles", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "AccessSections",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessSections", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "RoleSections",
                columns: table => new
                {
                    RoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SectionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Access = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleSections", x => new { x.RoleCode, x.SectionCode });
                    table.CheckConstraint("CK_RoleSection_Access", "\"Access\" IN ('None','Read','Write')");
                    table.ForeignKey(
                        name: "FK_RoleSections_AccessRoles_RoleCode",
                        column: x => x.RoleCode,
                        principalTable: "AccessRoles",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleSections_AccessSections_SectionCode",
                        column: x => x.SectionCode,
                        principalTable: "AccessSections",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('dashboard','داشبورد','دسترسی به بخش داشبورد',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('jobs','موقعیت‌های شغلی','دسترسی به بخش موقعیت‌های شغلی',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('templates','قالب مصاحبه','دسترسی به بخش قالب مصاحبه',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('questions','بانک سؤال','دسترسی به بخش بانک سؤال',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('interviews','مصاحبه‌ها','دسترسی به بخش مصاحبه‌ها',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('reports','گزارش و ارزیابی','دسترسی به بخش گزارش و ارزیابی',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('settings','تنظیمات سازمان','دسترسی به بخش تنظیمات سازمان',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('subscriptions','اشتراک و اعتبار','دسترسی به بخش اشتراک و اعتبار',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('tickets','پشتیبانی','دسترسی به بخش پشتیبانی',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessSections" ("Code","Name","Description","IsActive","Revision") VALUES ('team','اعضا و دعوت','دسترسی به بخش اعضا و دعوت',true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "AccessRoles" ("Code","Name","Description","IsActive","IsAdministrator","Revision") VALUES ('admin','ادمین','دسترسی کامل به بخش‌های سازمان',true,true,'11111111-1111-1111-1111-111111111111');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','dashboard','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','jobs','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','templates','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','questions','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','interviews','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','reports','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','settings','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','subscriptions','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','tickets','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('admin','team','Write');
INSERT INTO "AccessRoles" ("Code","Name","Description","IsActive","IsAdministrator","Revision") VALUES ('interview_manager','مدیر مصاحبه','مدیریت فرایند مصاحبه و ارزیابی و پشتیبانی',true,false,'11111111-1111-1111-1111-111111111111');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','dashboard','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','jobs','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','templates','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','questions','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','interviews','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','reports','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','settings','None');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','subscriptions','None');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','tickets','Write');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('interview_manager','team','None');
INSERT INTO "AccessRoles" ("Code","Name","Description","IsActive","IsAdministrator","Revision") VALUES ('viewer','ناظر','مشاهده اطلاعات بدون ایجاد یا ویرایش',true,false,'11111111-1111-1111-1111-111111111111');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','dashboard','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','jobs','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','templates','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','questions','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','interviews','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','reports','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','settings','None');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','subscriptions','None');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','tickets','Read');
INSERT INTO "RoleSections" ("RoleCode","SectionCode","Access") VALUES ('viewer','team','None');
CREATE FUNCTION hireyar_access_audit_immutable() RETURNS trigger LANGUAGE plpgsql AS $audit$
BEGIN RAISE EXCEPTION 'Access audit records are immutable.' USING ERRCODE = '23514'; END;
$audit$;
CREATE TRIGGER access_audit_immutable BEFORE UPDATE OR DELETE ON "AccessAudits" FOR EACH ROW EXECUTE FUNCTION hireyar_access_audit_immutable();
""");

            migrationBuilder.CreateIndex(
                name: "IX_MobileTeamInvitations_AccessRoleCode",
                table: "MobileTeamInvitations",
                column: "AccessRoleCode");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AccessRoleCode",
                table: "AspNetUsers",
                column: "AccessRoleCode");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAudits_ActorId",
                table: "AccessAudits",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAudits_OrganizationId_CreatedAtUtc",
                table: "AccessAudits",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleSections_SectionCode",
                table: "RoleSections",
                column: "SectionCode");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AccessRoles_AccessRoleCode",
                table: "AspNetUsers",
                column: "AccessRoleCode",
                principalTable: "AccessRoles",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MobileTeamInvitations_AccessRoles_AccessRoleCode",
                table: "MobileTeamInvitations",
                column: "AccessRoleCode",
                principalTable: "AccessRoles",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $guard$ BEGIN
IF EXISTS (SELECT 1 FROM "AccessAudits") OR EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "AccessRoleCode" IS NOT NULL) OR
   EXISTS (SELECT 1 FROM "MobileTeamInvitations" WHERE "AccessRoleCode" <> 'interview_manager' OR "Email" IS NOT NULL OR "Message" IS NOT NULL) THEN
  RAISE EXCEPTION 'Access history exists. Restore a backup instead of dropping access data.';
END IF;
END $guard$;
DROP TRIGGER access_audit_immutable ON "AccessAudits";
DROP FUNCTION hireyar_access_audit_immutable();
""");
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AccessRoles_AccessRoleCode",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_MobileTeamInvitations_AccessRoles_AccessRoleCode",
                table: "MobileTeamInvitations");

            migrationBuilder.DropTable(
                name: "AccessAudits");

            migrationBuilder.DropTable(
                name: "RoleSections");

            migrationBuilder.DropTable(
                name: "AccessRoles");

            migrationBuilder.DropTable(
                name: "AccessSections");

            migrationBuilder.DropIndex(
                name: "IX_MobileTeamInvitations_AccessRoleCode",
                table: "MobileTeamInvitations");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AccessRoleCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccessRoleCode",
                table: "MobileTeamInvitations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "MobileTeamInvitations");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "MobileTeamInvitations");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "MobileTeamInvitations");

            migrationBuilder.DropColumn(
                name: "AccessRoleCode",
                table: "AspNetUsers");
        }
    }
}
