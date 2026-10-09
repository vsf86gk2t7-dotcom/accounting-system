using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyAndRowVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
        name: "RowVersion",
        table: "SalesReturnInvoices",
        type: "rowversion",
        rowVersion: true,
        nullable: false,
        defaultValue: new byte[0]);

    migrationBuilder.AddColumn<byte[]>(
        name: "RowVersion",
        table: "SalesInvoices",
        type: "rowversion",
        rowVersion: true,
        nullable: false,
        defaultValue: new byte[0]);

    migrationBuilder.AddColumn<byte[]>(
        name: "RowVersion",
        table: "PurchaseInvoices",
        type: "rowversion",
        rowVersion: true,
        nullable: false,
        defaultValue: new byte[0]);

    migrationBuilder.AddColumn<int>(
        name: "EntryKind",
        table: "JournalEntries",
        type: "int",
        nullable: false,
        defaultValue: 0);

    migrationBuilder.Sql(@"
        UPDATE [JournalEntries]
        SET [EntryKind] = 1
        WHERE [Description] LIKE N'%تكلفة فاتورة%';
    ");

    migrationBuilder.CreateIndex(
        name: "IX_JournalEntries_Source_Kind_Unique",
        table: "JournalEntries",
        columns: new[] { "SourceType", "SourceId", "EntryKind" },
        unique: true,
        filter: "[SourceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_Source_Kind_Unique",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesReturnInvoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "EntryKind",
                table: "JournalEntries");
        }
    }
}
