using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPurposeToJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "JournalEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceType_SourceId_Purpose",
                table: "JournalEntries",
                columns: new[] { "SourceType", "SourceId", "Purpose" },
                unique: true,
                filter: "[SourceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_SourceType_SourceId_Purpose",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "JournalEntries");
        }
    }
}
