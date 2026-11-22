using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentChatScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiConversations_Project_User_Updated",
                table: "AiConversations");

            migrationBuilder.AddColumn<long>(
                name: "document_id",
                table: "AiConversations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_document_id",
                table: "AiConversations",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_Project_User_Updated",
                table: "AiConversations",
                columns: new[] { "project_id", "user_id", "document_id", "updated_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_AiConversations_Document",
                table: "AiConversations",
                column: "document_id",
                principalTable: "DocumentNodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiConversations_Document",
                table: "AiConversations");

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_document_id",
                table: "AiConversations");

            migrationBuilder.DropIndex(
                name: "IX_AiConversations_Project_User_Updated",
                table: "AiConversations");

            migrationBuilder.DropColumn(
                name: "document_id",
                table: "AiConversations");

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_Project_User_Updated",
                table: "AiConversations",
                columns: new[] { "project_id", "user_id", "updated_at" });
        }
    }
}
