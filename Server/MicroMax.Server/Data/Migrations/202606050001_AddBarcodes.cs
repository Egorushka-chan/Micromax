using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MicroMax.Server.Data.Migrations;

[DbContext(typeof(MicroMaxDbContext))]
[Migration("202606050001_AddBarcodes")]
public sealed class AddBarcodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Barcodes",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Value = table.Column<string>(maxLength: 160, nullable: false),
                Symbology = table.Column<string>(nullable: false),
                EntityType = table.Column<string>(nullable: false),
                EntityId = table.Column<int>(nullable: false),
                IsPrimary = table.Column<bool>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                CreatedByUserId = table.Column<int>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Barcodes", x => x.Id);
                table.ForeignKey(
                    name: "FK_Barcodes_AppUsers_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Barcodes_CreatedByUserId",
            table: "Barcodes",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Barcodes_Value",
            table: "Barcodes",
            column: "Value");

        migrationBuilder.CreateIndex(
            name: "IX_Barcodes_EntityType_EntityId",
            table: "Barcodes",
            columns: new[] { "EntityType", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_Barcodes_ActiveValue",
            table: "Barcodes",
            column: "Value",
            unique: true,
            filter: "\"IsActive\" = TRUE");

        migrationBuilder.CreateIndex(
            name: "IX_Barcodes_ActivePrimary",
            table: "Barcodes",
            columns: new[] { "EntityType", "EntityId" },
            unique: true,
            filter: "\"IsActive\" = TRUE AND \"IsPrimary\" = TRUE");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Barcodes");
    }
}
