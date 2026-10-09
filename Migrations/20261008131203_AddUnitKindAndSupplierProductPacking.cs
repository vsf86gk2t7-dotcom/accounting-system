using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitKindAndSupplierProductPacking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PiecesPerUnit",
                table: "Units",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierProductPackings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductPackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierProductPackings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductPackings_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductPackings_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPackings_ProductId",
                table: "SupplierProductPackings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPackings_Unique",
                table: "SupplierProductPackings",
                columns: new[] { "SupplierId", "ProductId", "UnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPackings_UnitId",
                table: "SupplierProductPackings",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierProductPackings");

            migrationBuilder.DropIndex(
                name: "IX_Units_Name",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "PiecesPerUnit",
                table: "Units");
        }
    }
}
