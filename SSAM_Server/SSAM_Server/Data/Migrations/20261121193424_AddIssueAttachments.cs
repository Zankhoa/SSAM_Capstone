using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IssueAttachments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    stored_file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueAttachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_IssueAttachments_Issue",
                        column: x => x.issue_id,
                        principalTable: "Issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueAttachments_UploadedBy",
                        column: x => x.uploaded_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueAttachments_Issue_Time",
                table: "IssueAttachments",
                columns: new[] { "issue_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueAttachments_uploaded_by_id",
                table: "IssueAttachments",
                column: "uploaded_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueAttachments");
        }
    }
}
