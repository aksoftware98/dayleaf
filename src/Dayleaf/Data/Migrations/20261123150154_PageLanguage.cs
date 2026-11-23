using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dayleaf.Data.Migrations
{
    /// <inheritdoc />
    public partial class PageLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultLanguage",
                table: "Spaces",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Pages",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultLanguage",
                table: "Spaces");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Pages");
        }
    }
}
