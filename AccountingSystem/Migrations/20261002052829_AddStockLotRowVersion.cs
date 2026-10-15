using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
	/// <inheritdoc />
	public partial class AddStockLotRowVersion : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<byte[]>(
				name: "RowVersion",
				table: "StockLots",
				type: "rowversion",
				rowVersion: true,
				nullable: false
				);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "RowVersion",
				table: "StockLots");
		}
	}
}
