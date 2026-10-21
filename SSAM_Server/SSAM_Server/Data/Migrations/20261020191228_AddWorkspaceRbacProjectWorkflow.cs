using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceRbacProjectWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceMembers_Workspaces",
                table: "WorkspaceMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_Companies",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_company_id",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMembers_user_id",
                table: "WorkspaceMembers");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_Token",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "is_personal",
                table: "Workspaces");

            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at",
                table: "Workspaces",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "Workspaces",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "created_by",
                table: "Workspaces",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "Workspaces",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "Workspaces",
                type: "datetime2(3)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "WorkspaceMembers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "role_id",
                table: "MembershipRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "workspace_role_id",
                table: "MembershipRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "CompanyRoles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "CompanyRoles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "CompanyRoles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "TENANT");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "CompanyRoles",
                type: "datetime2(3)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    group_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_sensitive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.id);
                    table.CheckConstraint("CK_Permissions_Scope", "[scope] IN ('TENANT', 'WORKSPACE', 'PROJECT')");
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    workspace_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    sdlc_method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "CUSTOM"),
                    ai_enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    archived_at = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.id);
                    table.CheckConstraint("CK_Projects_Sdlc", "[sdlc_method] IN ('SCRUM', 'KANBAN', 'CUSTOM')");
                    table.ForeignKey(
                        name: "FK_Projects_CreatedBy",
                        column: x => x.created_by,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_Workspace",
                        column: x => x.workspace_id,
                        principalTable: "Workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permission",
                        column: x => x.permission_id,
                        principalTable: "Permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Role",
                        column: x => x.role_id,
                        principalTable: "CompanyRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    joined_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => x.id);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Role",
                        column: x => x.role_id,
                        principalTable: "CompanyRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_User",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPhases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    position = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPhases", x => x.id);
                    table.ForeignKey(
                        name: "FK_ProjectPhases_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStatuses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    phase_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_initial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStatuses", x => x.id);
                    table.CheckConstraint("CK_WorkflowStatuses_Category", "[category] IN ('TODO', 'IN_PROGRESS', 'DONE')");
                    table.ForeignKey(
                        name: "FK_WorkflowStatuses_Phase",
                        column: x => x.phase_id,
                        principalTable: "ProjectPhases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowStatuses_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    phase_id = table.Column<long>(type: "bigint", nullable: false),
                    from_status_id = table.Column<long>(type: "bigint", nullable: false),
                    to_status_id = table.Column<long>(type: "bigint", nullable: false),
                    require_comment = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    comment_hint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    condition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.id);
                    table.CheckConstraint("CK_WorkflowTransitions_NotSelf", "[from_status_id] <> [to_status_id]");
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_From",
                        column: x => x.from_status_id,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_Phase",
                        column: x => x.phase_id,
                        principalTable: "ProjectPhases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_To",
                        column: x => x.to_status_id,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionRoles",
                columns: table => new
                {
                    transition_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionRoles", x => new { x.transition_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionRoles_Role",
                        column: x => x.role_id,
                        principalTable: "CompanyRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionRoles_Transition",
                        column: x => x.transition_id,
                        principalTable: "WorkflowTransitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 1L, "company.update", "Sửa tên, mã số thuế, địa chỉ, logo của công ty.", "Công ty", true, true, "Cập nhật thông tin công ty", "TENANT", 1 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 2L, "user.view", "Xem hồ sơ và trạng thái của mọi tài khoản trong công ty.", "Người dùng", true, "Xem danh sách người dùng", "TENANT", 2 },
                    { 3L, "user.invite", "Gửi email mời tham gia công ty.", "Người dùng", true, "Mời người dùng vào công ty", "TENANT", 3 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 4L, "user.assign_role", "Gán / thu hồi role cấp công ty.", "Người dùng", true, true, "Đổi role cấp công ty của người dùng", "TENANT", 4 },
                    { 5L, "user.deactivate", "Khoá đăng nhập của nhân viên đã nghỉ.", "Người dùng", true, true, "Vô hiệu hoá / kích hoạt tài khoản", "TENANT", 5 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[] { 6L, "role.view", "Xem role và ma trận quyền của công ty.", "Role & Phân quyền", true, "Xem danh mục role", "TENANT", 6 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 7L, "role.manage", "Định nghĩa role mới và chọn quyền cho từng role.", "Role & Phân quyền", true, true, "Tạo / sửa / bật-tắt role và quyền của role", "TENANT", 7 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 8L, "workspace.create", "Khởi tạo không gian làm việc mới trong công ty.", "Workspace", true, "Tạo workspace mới", "TENANT", 8 },
                    { 9L, "workspace.view_all", "Thấy cả workspace mình không phải thành viên (chỉ xem).", "Workspace", true, "Xem mọi workspace trong công ty", "TENANT", 9 },
                    { 10L, "workspace.update", null, "Workspace", true, "Sửa tên / mã / mô tả workspace", "WORKSPACE", 10 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 11L, "workspace.archive", "Chuyển toàn bộ dữ liệu workspace sang chỉ đọc.", "Workspace", true, true, "Lưu trữ / khôi phục workspace", "WORKSPACE", 11 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[] { 12L, "workspace.member.view", null, "Thành viên", true, "Xem thành viên workspace", "WORKSPACE", 12 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 13L, "workspace.member.manage", null, "Thành viên", true, true, "Thêm / gỡ / đổi role thành viên workspace", "WORKSPACE", 13 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 14L, "project.create", "Vai trò \"Project Initiator\" trong SRS.", "Dự án", true, "Tạo dự án trong workspace", "WORKSPACE", 14 },
                    { 15L, "project.view_all", null, "Dự án", true, "Xem mọi dự án trong workspace", "WORKSPACE", 15 },
                    { 16L, "project.update", null, "Dự án", true, "Sửa tên / mô tả dự án", "PROJECT", 16 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 17L, "project.archive", "Đóng băng mọi tài liệu / issue sang chỉ đọc.", "Dự án", true, true, "Lưu trữ / khôi phục dự án", "PROJECT", 17 },
                    { 18L, "project.ai.toggle", "Cho phép dùng AI sinh tài liệu / testcase trong dự án.", "Dự án", true, true, "Bật / tắt module AI (BA/QA)", "PROJECT", 18 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[] { 19L, "project.member.view", null, "Thành viên dự án", true, "Xem thành viên dự án", "PROJECT", 19 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 20L, "project.member.manage", null, "Thành viên dự án", true, true, "Thêm / gỡ / đổi role thành viên dự án", "PROJECT", 20 },
                    { 21L, "project.workflow.manage", "Thêm / sửa phase, trạng thái và transition rule (UC-44~50).", "Workflow", true, true, "Cấu hình phase, trạng thái, luồng chuyển", "PROJECT", 21 }
                });

            // Backfill existing data before adding UQ_Workspaces_Company_Code and FK_Workspaces_CreatedBy
            migrationBuilder.Sql(@"
                -- 1. Backfill Workspaces.code and created_by for any existing rows
                UPDATE w
                SET w.code = CONCAT('ws-', w.id),
                    w.created_by = COALESCE(
                        (SELECT TOP 1 wm.user_id FROM WorkspaceMembers wm WHERE wm.workspace_id = w.id ORDER BY wm.joined_at ASC),
                        (SELECT TOP 1 cm.user_id FROM CompanyMembers cm WHERE cm.company_id = w.company_id ORDER BY cm.joined_at ASC),
                        (SELECT TOP 1 u.id FROM Users u ORDER BY u.id ASC)
                    )
                FROM Workspaces w
                WHERE w.code = '' OR w.created_by = 0;

                -- 2. Update existing CompanyRoles scope & description
                UPDATE CompanyRoles SET scope = 'TENANT', description = N'Toàn quyền quản trị công ty, tổ chức, không gian làm việc và phân quyền.' WHERE code = 'TENANT_ADMIN';
                UPDATE CompanyRoles SET scope = 'TENANT', description = N'Thành viên công ty cơ bản.' WHERE code = 'MEMBER';

                -- 3. Seed missing system roles (WORKSPACE & PROJECT scopes) for existing companies
                INSERT INTO CompanyRoles (company_id, code, name, description, scope, is_system, is_active, created_at)
                SELECT c.id, v.code, v.name, v.description, v.scope, 1, 1, SYSUTCDATETIME()
                FROM Companies c
                CROSS JOIN (VALUES
                    ('WORKSPACE_ADMIN',   N'Workspace Admin',   N'Quản trị viên không gian làm việc.',       'WORKSPACE'),
                    ('PROJECT_INITIATOR', N'Project Initiator', N'Thành viên được phép khởi tạo dự án mới.', 'WORKSPACE'),
                    ('WORKSPACE_MEMBER',  N'Workspace Member',  N'Thành viên không gian làm việc.',          'WORKSPACE'),
                    ('PROJECT_MANAGER',   N'Project Manager',   N'Quản lý dự án (PM).',                      'PROJECT'),
                    ('TECH_LEAD',         N'Tech Lead',         N'Trưởng nhóm kỹ thuật.',                    'PROJECT'),
                    ('BA',                N'Business Analyst',  N'Chuyên viên phân tích nghiệp vụ.',         'PROJECT'),
                    ('DEVELOPER',         N'Developer',         N'Lập trình viên.',                          'PROJECT'),
                    ('QA',                N'QA / Tester',       N'Kiểm thử viên.',                           'PROJECT')
                ) AS v(code, name, description, scope)
                WHERE NOT EXISTS (
                    SELECT 1 FROM CompanyRoles cr WHERE cr.company_id = c.id AND cr.code = v.code
                );

                -- 4. Seed default RolePermissions for existing companies
                INSERT INTO RolePermissions (role_id, permission_id)
                SELECT cr.id, p.id
                FROM CompanyRoles cr
                JOIN Permissions p ON (
                    (cr.code = 'TENANT_ADMIN' AND p.scope = 'TENANT')
                    OR (cr.code = 'MEMBER' AND p.code IN ('user.view'))
                    OR (cr.code = 'WORKSPACE_ADMIN' AND p.scope = 'WORKSPACE')
                    OR (cr.code = 'PROJECT_INITIATOR' AND p.code IN ('workspace.member.view', 'project.create', 'project.view_all'))
                    OR (cr.code = 'WORKSPACE_MEMBER' AND p.code IN ('workspace.member.view', 'project.view_all'))
                    OR (cr.code = 'PROJECT_MANAGER' AND p.scope = 'PROJECT')
                    OR (cr.code IN ('TECH_LEAD', 'BA', 'DEVELOPER', 'QA') AND p.code IN ('project.member.view'))
                )
                WHERE NOT EXISTS (
                    SELECT 1 FROM RolePermissions rp WHERE rp.role_id = cr.id AND rp.permission_id = p.id
                );

                -- 5. Backfill WorkspaceMembers.role_id for existing company workspaces
                UPDATE wm
                SET wm.role_id = cr.id
                FROM WorkspaceMembers wm
                JOIN Workspaces w ON w.id = wm.workspace_id
                JOIN CompanyRoles cr ON cr.company_id = w.company_id
                    AND cr.code = CASE WHEN wm.user_id = w.created_by THEN 'WORKSPACE_ADMIN' ELSE 'WORKSPACE_MEMBER' END
                WHERE w.company_id IS NOT NULL AND wm.role_id IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_Company_Active",
                table: "Workspaces",
                columns: new[] { "company_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_created_by",
                table: "Workspaces",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "UQ_Workspaces_Company_Code",
                table: "Workspaces",
                columns: new[] { "company_id", "code" },
                unique: true,
                filter: "[company_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_RoleId",
                table: "WorkspaceMembers",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_User_Active",
                table: "WorkspaceMembers",
                columns: new[] { "user_id", "is_active" });

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
                name: "IX_MembershipRequests_workspace_role_id",
                table: "MembershipRequests",
                column: "workspace_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRoles_Company_Scope",
                table: "CompanyRoles",
                columns: new[] { "company_id", "scope", "is_active" });

            migrationBuilder.CreateIndex(
                name: "UQ_CompanyRoles_Company_Code",
                table: "CompanyRoles",
                columns: new[] { "company_id", "code" },
                unique: true,
                filter: "[code] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompanyRoles_Scope",
                table: "CompanyRoles",
                sql: "[scope] IN ('TENANT', 'WORKSPACE', 'PROJECT')");

            migrationBuilder.CreateIndex(
                name: "UQ_Permissions_Code",
                table: "Permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_Project_Active",
                table: "ProjectMembers",
                columns: new[] { "project_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_RoleId",
                table: "ProjectMembers",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_User_Active",
                table: "ProjectMembers",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "UQ_ProjectMembers_Project_User",
                table: "ProjectMembers",
                columns: new[] { "project_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhases_Project_Order",
                table: "ProjectPhases",
                columns: new[] { "project_id", "is_active", "position" });

            migrationBuilder.CreateIndex(
                name: "UQ_ProjectPhases_Project_Name",
                table: "ProjectPhases",
                columns: new[] { "project_id", "name" },
                unique: true,
                filter: "[is_active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_created_by",
                table: "Projects",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Workspace_Active",
                table: "Projects",
                columns: new[] { "workspace_id", "is_active", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "UQ_Projects_Workspace_Key",
                table: "Projects",
                columns: new[] { "workspace_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_Phase_Order",
                table: "WorkflowStatuses",
                columns: new[] { "phase_id", "is_active", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_project_id",
                table: "WorkflowStatuses",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "UQ_WorkflowStatuses_Phase_Initial",
                table: "WorkflowStatuses",
                column: "phase_id",
                unique: true,
                filter: "[is_initial] = 1 AND [is_active] = 1");

            migrationBuilder.CreateIndex(
                name: "UQ_WorkflowStatuses_Phase_Name",
                table: "WorkflowStatuses",
                columns: new[] { "phase_id", "name" },
                unique: true,
                filter: "[is_active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionRoles_RoleId",
                table: "WorkflowTransitionRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_From",
                table: "WorkflowTransitions",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_project_id",
                table: "WorkflowTransitions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_To",
                table: "WorkflowTransitions",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "UQ_WorkflowTransitions_Pair",
                table: "WorkflowTransitions",
                columns: new[] { "phase_id", "from_status_id", "to_status_id" },
                unique: true);

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
                name: "FK_WorkspaceMembers_Role",
                table: "WorkspaceMembers",
                column: "role_id",
                principalTable: "CompanyRoles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkspaceMembers_Workspaces",
                table: "WorkspaceMembers",
                column: "workspace_id",
                principalTable: "Workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_Companies",
                table: "Workspaces",
                column: "company_id",
                principalTable: "Companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_CreatedBy",
                table: "Workspaces",
                column: "created_by",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipRequests_Role",
                table: "MembershipRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MembershipRequests_WorkspaceRole",
                table: "MembershipRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceMembers_Role",
                table: "WorkspaceMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkspaceMembers_Workspaces",
                table: "WorkspaceMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_Companies",
                table: "Workspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_CreatedBy",
                table: "Workspaces");

            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "WorkflowStatuses");

            migrationBuilder.DropTable(
                name: "ProjectPhases");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_Company_Active",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_created_by",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "UQ_Workspaces_Company_Code",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMembers_RoleId",
                table: "WorkspaceMembers");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMembers_User_Active",
                table: "WorkspaceMembers");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_role_id",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_Token",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_workspace_role_id",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_CompanyRoles_Company_Scope",
                table: "CompanyRoles");

            migrationBuilder.DropIndex(
                name: "UQ_CompanyRoles_Company_Code",
                table: "CompanyRoles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompanyRoles_Scope",
                table: "CompanyRoles");

            migrationBuilder.DropColumn(
                name: "archived_at",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "code",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "WorkspaceMembers");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "workspace_role_id",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "description",
                table: "CompanyRoles");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "CompanyRoles");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "CompanyRoles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "CompanyRoles");

            migrationBuilder.AddColumn<bool>(
                name: "is_personal",
                table: "Workspaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_company_id",
                table: "Workspaces",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_user_id",
                table: "WorkspaceMembers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_Token",
                table: "MembershipRequests",
                column: "token",
                filter: "[token] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkspaceMembers_Workspaces",
                table: "WorkspaceMembers",
                column: "workspace_id",
                principalTable: "Workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_Companies",
                table: "Workspaces",
                column: "company_id",
                principalTable: "Companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
