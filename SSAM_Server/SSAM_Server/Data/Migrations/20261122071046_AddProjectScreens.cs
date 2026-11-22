using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectScreens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectScreens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    figma_url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    image_file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    image_stored_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    image_content_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    document_id = table.Column<long>(type: "bigint", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectScreens", x => x.id);
                    table.CheckConstraint("CK_ProjectScreens_Kind", "[kind] IN ('SCREEN','LAYOUT')");
                    table.ForeignKey(
                        name: "FK_ProjectScreens_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ProjectScreens_Documents",
                        column: x => x.document_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ProjectScreens_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectScreens_created_by_id",
                table: "ProjectScreens",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectScreens_document_id",
                table: "ProjectScreens",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectScreens_Project_Time",
                table: "ProjectScreens",
                columns: new[] { "project_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectScreens");
        }
    }
}
