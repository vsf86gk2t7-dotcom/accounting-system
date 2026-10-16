using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalPurchaseInvoiceToReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalPurchaseInvoiceId",
                table: "PurchaseReturnInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseInvoiceItemId",
                table: "PurchaseReturnInvoiceItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnInvoices_OriginalPurchaseInvoiceId",
                table: "PurchaseReturnInvoices",
                column: "OriginalPurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnInvoiceItems_PurchaseInvoiceItemId",
                table: "PurchaseReturnInvoiceItems",
                column: "PurchaseInvoiceItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnInvoiceItems_PurchaseInvoiceItems_PurchaseInvoiceItemId",
                table: "PurchaseReturnInvoiceItems",
                column: "PurchaseInvoiceItemId",
                principalTable: "PurchaseInvoiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnInvoices_PurchaseInvoices_OriginalPurchaseInvoiceId",
                table: "PurchaseReturnInvoices",
                column: "OriginalPurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnInvoiceItems_PurchaseInvoiceItems_PurchaseInvoiceItemId",
                table: "PurchaseReturnInvoiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnInvoices_PurchaseInvoices_OriginalPurchaseInvoiceId",
                table: "PurchaseReturnInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturnInvoices_OriginalPurchaseInvoiceId",
                table: "PurchaseReturnInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturnInvoiceItems_PurchaseInvoiceItemId",
                table: "PurchaseReturnInvoiceItems");

            migrationBuilder.DropColumn(
                name: "OriginalPurchaseInvoiceId",
                table: "PurchaseReturnInvoices");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceItemId",
                table: "PurchaseReturnInvoiceItems");
        }
    }
}
