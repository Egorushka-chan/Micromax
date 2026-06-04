using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MicroMax.Server.Data.Migrations;

[DbContext(typeof(MicroMaxDbContext))]
[Migration("202606040001_AddAuthAndWarehouseRoles")]
public sealed class AddAuthAndWarehouseRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Code = table.Column<string>(maxLength: 32, nullable: false),
                Name = table.Column<string>(maxLength: 120, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Roles", x => x.Id));

        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "AppUsers",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            table: "AppUsers",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAt",
            table: "AppUsers",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "NOW()");

        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "AppUsers",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<int>(
            name: "WarehouseId",
            table: "WarehouseOperations",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(nullable: false),
                TokenHash = table.Column<string>(maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_RefreshTokens_AppUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WarehouseUsers",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                WarehouseId = table.Column<int>(nullable: false),
                UserId = table.Column<int>(nullable: false),
                RoleId = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WarehouseUsers", x => x.Id);
                table.ForeignKey(
                    name: "FK_WarehouseUsers_AppUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_WarehouseUsers_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_WarehouseUsers_Warehouses_WarehouseId",
                    column: x => x.WarehouseId,
                    principalTable: "Warehouses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_Email",
            table: "AppUsers",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Code",
            table: "Roles",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenHash",
            table: "RefreshTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId",
            table: "RefreshTokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseOperations_WarehouseId",
            table: "WarehouseOperations",
            column: "WarehouseId");

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseUsers_RoleId",
            table: "WarehouseUsers",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseUsers_UserId",
            table: "WarehouseUsers",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_WarehouseUsers_WarehouseId_UserId",
            table: "WarehouseUsers",
            columns: new[] { "WarehouseId", "UserId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RefreshTokens");
        migrationBuilder.DropTable(name: "WarehouseUsers");
        migrationBuilder.DropTable(name: "Roles");
        migrationBuilder.DropIndex(name: "IX_AppUsers_Email", table: "AppUsers");
        migrationBuilder.DropIndex(name: "IX_WarehouseOperations_WarehouseId", table: "WarehouseOperations");
        migrationBuilder.DropColumn(name: "Email", table: "AppUsers");
        migrationBuilder.DropColumn(name: "PasswordHash", table: "AppUsers");
        migrationBuilder.DropColumn(name: "CreatedAt", table: "AppUsers");
        migrationBuilder.DropColumn(name: "IsActive", table: "AppUsers");
        migrationBuilder.DropColumn(name: "WarehouseId", table: "WarehouseOperations");
    }
}
