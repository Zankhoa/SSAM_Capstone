using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitCompanyInvitationsFromMembershipRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyInvitations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    role_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    role_id = table.Column<long>(type: "bigint", nullable: true),
                    workspace_id = table.Column<long>(type: "bigint", nullable: true),
                    workspace_role_id = table.Column<long>(type: "bigint", nullable: true),
                    department_id = table.Column<long>(type: "bigint", nullable: true),
                    token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    expires_at = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInvitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_CompanyInvitations_Companies",
                        column: x => x.company_id,
                        principalTable: "Companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyInvitations_Departments",
                        column: x => x.department_id,
                        principalTable: "Departments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_CompanyInvitations_Role",
                        column: x => x.role_id,
                        principalTable: "CompanyRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyInvitations_WorkspaceRole",
                        column: x => x.workspace_role_id,
                        principalTable: "CompanyRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyInvitations_Workspaces",
                        column: x => x.workspace_id,
                        principalTable: "Workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(@"
                INSERT INTO [CompanyInvitations] ([company_id], [email], [role_name], [role_id], [workspace_id], [workspace_role_id], [department_id], [token], [status], [expires_at], [created_at])
                SELECT [company_id], [email], [role_name], [role_id], [workspace_id], [workspace_role_id], [department_id], [token], [status], [expires_at], [created_at]
                FROM [MembershipRequests]
                WHERE [source] = 'admin_invite' AND [token] IS NOT NULL;

                DELETE FROM [MembershipRequests] WHERE [source] = 'admin_invite';
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipRequests_Departments",
                table: "MembershipRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipRequests_Role",
                table: "MembershipRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipRequests_WorkspaceRole",
                table: "MembershipRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipRequests_Workspaces",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_department_id",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_role_id",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_Token",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_workspace_id",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_workspace_role_id",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "role_name",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "source",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "token",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "workspace_role_id",
                table: "MembershipRequests");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_company_id",
                table: "CompanyInvitations",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_department_id",
                table: "CompanyInvitations",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_role_id",
                table: "CompanyInvitations",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_Token",
                table: "CompanyInvitations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_workspace_id",
                table: "CompanyInvitations",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvitations_workspace_role_id",
                table: "CompanyInvitations",
                column: "workspace_role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyInvitations");

            migrationBuilder.AddColumn<long>(
                name: "department_id",
                table: "MembershipRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                table: "MembershipRequests",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "role_id",
                table: "MembershipRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role_name",
                table: "MembershipRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "MembershipRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "token",
                table: "MembershipRequests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "workspace_id",
                table: "MembershipRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "workspace_role_id",
                table: "MembershipRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_department_id",
                table: "MembershipRequests",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_role_id",
                table: "MembershipRequests",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_Token",
                table: "MembershipRequests",
                column: "token",
                unique: true,
                filter: "[token] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_workspace_id",
                table: "MembershipRequests",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_workspace_role_id",
                table: "MembershipRequests",
                column: "workspace_role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipRequests_Departments",
                table: "MembershipRequests",
                column: "department_id",
                principalTable: "Departments",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipRequests_Role",
                table: "MembershipRequests",
                column: "role_id",
                principalTable: "CompanyRoles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipRequests_WorkspaceRole",
                table: "MembershipRequests",
                column: "workspace_role_id",
                principalTable: "CompanyRoles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipRequests_Workspaces",
                table: "MembershipRequests",
                column: "workspace_id",
                principalTable: "Workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
