using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistAiConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiConversations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversations", x => x.id);
                    table.ForeignKey(
                        name: "FK_AiConversations_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiConversations_User",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiConversationMessages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    conversation_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    citations_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_real_ai = table.Column<bool>(type: "bit", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversationMessages", x => x.id);
                    table.CheckConstraint("CK_AiConversationMessages_CitationsJson", "[citations_json] IS NULL OR ISJSON([citations_json]) = 1");
                    table.CheckConstraint("CK_AiConversationMessages_Role", "[role] IN ('user', 'assistant')");
                    table.ForeignKey(
                        name: "FK_AiConversationMessages_Conversation",
                        column: x => x.conversation_id,
                        principalTable: "AiConversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationMessages_Conversation_Created",
                table: "AiConversationMessages",
                columns: new[] { "conversation_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_Project_User_Updated",
                table: "AiConversations",
                columns: new[] { "project_id", "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_user_id",
                table: "AiConversations",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiConversationMessages");

            migrationBuilder.DropTable(
                name: "AiConversations");
        }
    }
}
