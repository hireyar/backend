using HireYar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace HireYar.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260927090000_AddMobileOtp")]
public sealed class AddMobileOtp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "Email", table: "AspNetUsers", type: "character varying(256)", maxLength: 256, nullable: true, oldClrType: typeof(string), oldType: "character varying(256)", oldMaxLength: 256);
        migrationBuilder.AlterColumn<string>(name: "NormalizedEmail", table: "AspNetUsers", type: "character varying(256)", maxLength: 256, nullable: true, oldClrType: typeof(string), oldType: "character varying(256)", oldMaxLength: 256);
        migrationBuilder.AlterColumn<string>(name: "PhoneNumber", table: "AspNetUsers", type: "character varying(11)", maxLength: 11, nullable: true, oldClrType: typeof(string), oldType: "text", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_AspNetUsers_PhoneNumber", table: "AspNetUsers", column: "PhoneNumber", unique: true, filter: "\"PhoneNumber\" IS NOT NULL");
        migrationBuilder.CreateTable(name: "OtpChallenges", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Mobile = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
            CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            Delivered = table.Column<bool>(type: "boolean", nullable: false),
            FailedAttempts = table.Column<int>(type: "integer", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_OtpChallenges", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_OtpChallenges_Mobile", table: "OtpChallenges", column: "Mobile", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OtpChallenges");
        migrationBuilder.DropIndex(name: "IX_AspNetUsers_PhoneNumber", table: "AspNetUsers");
        migrationBuilder.AlterColumn<string>(name: "PhoneNumber", table: "AspNetUsers", type: "text", nullable: true, oldClrType: typeof(string), oldType: "character varying(11)", oldMaxLength: 11, oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "Email", table: "AspNetUsers", type: "character varying(256)", maxLength: 256, nullable: false, oldClrType: typeof(string), oldType: "character varying(256)", oldMaxLength: 256, oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "NormalizedEmail", table: "AspNetUsers", type: "character varying(256)", maxLength: 256, nullable: false, oldClrType: typeof(string), oldType: "character varying(256)", oldMaxLength: 256, oldNullable: true);
    }
}
