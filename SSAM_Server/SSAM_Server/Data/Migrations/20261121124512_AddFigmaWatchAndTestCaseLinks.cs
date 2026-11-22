using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFigmaWatchAndTestCaseLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FigmaFileWatches",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    file_key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    last_version = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    last_modified = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    last_checked_at = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    last_changed_at = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FigmaFileWatches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseFigmaLinks",
                columns: table => new
                {
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    file_key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    node_id = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true),
                    url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseFigmaLinks", x => new { x.test_case_id, x.file_key });
                    table.ForeignKey(
                        name: "FK_TestCaseFigmaLinks_Case",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_FigmaFileWatches_FileKey",
                table: "FigmaFileWatches",
                column: "file_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseFigmaLinks_FileKey",
                table: "TestCaseFigmaLinks",
                column: "file_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FigmaFileWatches");

            migrationBuilder.DropTable(
                name: "TestCaseFigmaLinks");
        }
    }
}
