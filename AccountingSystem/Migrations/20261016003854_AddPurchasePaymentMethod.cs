using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.AddColumn<int>(
                name: "CashAccountId",
                table: "PurchaseInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentDetails",
                table: "PurchaseInvoices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "PurchaseInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CashAccountId",
                table: "PurchaseInvoices",
                column: "CashAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_CashAccounts_CashAccountId",
                table: "PurchaseInvoices",
                column: "CashAccountId",
                principalTable: "CashAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_CashAccounts_CashAccountId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_CashAccountId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "CashAccountId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentDetails",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PurchaseInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices",
                column: "InvoiceNumber",
                unique: true);
        }
    }
}
