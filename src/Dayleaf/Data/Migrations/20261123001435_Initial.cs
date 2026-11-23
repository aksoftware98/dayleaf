using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dayleaf.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalName = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultTheme = table.Column<string>(type: "TEXT", nullable: false),
                    CoverMediaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    SheetCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ShareToken = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pages_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Elements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Sheet = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    W = table.Column<double>(type: "REAL", nullable: false),
                    H = table.Column<double>(type: "REAL", nullable: false),
                    Rotation = table.Column<double>(type: "REAL", nullable: false),
                    Z = table.Column<int>(type: "INTEGER", nullable: false),
                    DataJson = table.Column<string>(type: "TEXT", nullable: false),
                    PlainText = table.Column<string>(type: "TEXT", nullable: true),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LinkTargetPageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Elements_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seq = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SubmittedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Elements_LinkTargetPageId",
                table: "Elements",
                column: "LinkTargetPageId");

            migrationBuilder.CreateIndex(
                name: "IX_Elements_MediaId",
                table: "Elements",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Elements_PageId",
                table: "Elements",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_Elements_SectionId",
                table: "Elements",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_Sha256",
                table: "Media",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pages_Date",
                table: "Pages",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_ShareToken",
                table: "Pages",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pages_SpaceId_Date",
                table: "Pages",
                columns: new[] { "SpaceId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_PageId_Seq",
                table: "Sections",
                columns: new[] { "PageId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_Slug",
                table: "Spaces",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Elements");

            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.DropTable(
                name: "Spaces");
        }
    }
}
