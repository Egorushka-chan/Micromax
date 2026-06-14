using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MicroMax.Server.Data.Migrations;

[DbContext(typeof(MicroMaxDbContext))]
[Migration("202606150001_AddWebPanelAccess")]
public sealed class AddWebPanelAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "CanAccessWebPanel",
            table: "AppUsers",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CanAccessWebPanel",
            table: "AppUsers");
    }
}
