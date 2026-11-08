using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFe20261108_TestV2AndPhaseGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TestCaseIssueLinks");
            migrationBuilder.DropTable(name: "TestCaseRequirementLinks");
            migrationBuilder.DropTable(name: "TestRunDefects");
            migrationBuilder.DropTable(name: "TestRunHistory");
            migrationBuilder.DropTable(name: "TestRuns");
            migrationBuilder.DropTable(name: "TestCycles");
            migrationBuilder.DropTable(name: "TestCases");
            migrationBuilder.DropTable(name: "TestFolders");

            migrationBuilder.Sql("DELETE FROM ProjectSequences WHERE scope = 'TESTCASE';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentNodes_DocType",
                table: "DocumentNodes");

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "ProjectPhases",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "completed_by_id",
                table: "ProjectPhases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exit_criteria",
                table: "ProjectPhases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_status",
                table: "ProjectPhases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "UPCOMING");

            migrationBuilder.AddColumn<string>(
                name: "override_reason",
                table: "ProjectPhases",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectPhaseLogs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    phase_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    actor_id = table.Column<long>(type: "bigint", nullable: false),
                    at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    unmet_criteria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPhaseLogs", x => x.id);
                    table.CheckConstraint("CK_ProjectPhaseLogs_Action", "[action] IN (N'COMPLETE', N'OVERRIDE_COMPLETE', N'REOPEN')");
                    table.ForeignKey(
                        name: "FK_ProjectPhaseLogs_Actor",
                        column: x => x.actor_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ProjectPhaseLogs_Phase",
                        column: x => x.phase_id,
                        principalTable: "ProjectPhases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectPhaseLogs_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestPlans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                    handover_by_id = table.Column<long>(type: "bigint", nullable: true),
                    handover_at = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    handover_note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    sheet_layout = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestPlans", x => x.id);
                    table.CheckConstraint("CK_TestPlans_Layout_Json", "[sheet_layout] IS NULL OR ISJSON([sheet_layout]) = 1");
                    table.CheckConstraint("CK_TestPlans_Status", "[status] IN (N'OPEN', N'HANDED_OVER')");
                    table.ForeignKey(
                        name: "FK_TestPlans_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestPlans_HandoverBy",
                        column: x => x.handover_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestPlans_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestEnvironments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    plan_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, defaultValue: ""),
                    position = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestEnvironments", x => x.id);
                    table.ForeignKey(
                        name: "FK_TestEnvironments_Plan",
                        column: x => x.plan_id,
                        principalTable: "TestPlans",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestModules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    plan_id = table.Column<long>(type: "bigint", nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    short_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    position = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    case_seq = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestModules", x => x.id);
                    table.ForeignKey(
                        name: "FK_TestModules_Document",
                        column: x => x.document_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestModules_Plan",
                        column: x => x.plan_id,
                        principalTable: "TestPlans",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestSections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    module_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    anchor_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    position = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestSections", x => x.id);
                    table.ForeignKey(
                        name: "FK_TestSections_Module",
                        column: x => x.module_id,
                        principalTable: "TestModules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestSections_Parent",
                        column: x => x.parent_id,
                        principalTable: "TestSections",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestCases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    plan_id = table.Column<long>(type: "bigint", nullable: false),
                    module_id = table.Column<long>(type: "bigint", nullable: false),
                    section_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    preconditions = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    steps = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    actual = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    ai_suggested_reqs = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    priority = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "P1"),
                    category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "FUNCTIONAL"),
                    browser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    custom_fields = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    position = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    bug_issue_id = table.Column<long>(type: "bigint", nullable: true),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.id);
                    table.CheckConstraint("CK_TestCases_Category", "[category] IN (N'FUNCTIONAL', N'BOUNDARY', N'NEGATIVE', N'UI', N'SECURITY', N'PERFORMANCE')");
                    table.CheckConstraint("CK_TestCases_Custom_Json", "ISJSON([custom_fields]) = 1");
                    table.CheckConstraint("CK_TestCases_Priority", "[priority] IN (N'P0', N'P1', N'P2', N'P3')");
                    table.CheckConstraint("CK_TestCases_Status", "[status] IN (N'ACTIVE', N'DELETED')");
                    table.CheckConstraint("CK_TestCases_Steps_Json", "ISJSON([steps]) = 1");
                    table.ForeignKey(
                        name: "FK_TestCases_BugIssue",
                        column: x => x.bug_issue_id,
                        principalTable: "Issues",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_Module",
                        column: x => x.module_id,
                        principalTable: "TestModules",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_Plan",
                        column: x => x.plan_id,
                        principalTable: "TestPlans",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_Project",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_Section",
                        column: x => x.section_id,
                        principalTable: "TestSections",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestCaseRequirementLinks",
                columns: table => new
                {
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    req_uid = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    req_hash = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: ""),
                    changed_version_label = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    linked_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseRequirementLinks", x => new { x.test_case_id, x.document_id, x.req_uid });
                    table.ForeignKey(
                        name: "FK_TestCaseReqLinks_Document",
                        column: x => x.document_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCaseReqLinks_TestCase",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestBuilds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    env_id = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    deployed_at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    deployed_by_id = table.Column<long>(type: "bigint", nullable: false),
                    fixed_issue_ids = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestBuilds", x => x.id);
                    table.CheckConstraint("CK_TestBuilds_FixedIssues_Json", "ISJSON([fixed_issue_ids]) = 1");
                    table.ForeignKey(
                        name: "FK_TestBuilds_DeployedBy",
                        column: x => x.deployed_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestBuilds_Env",
                        column: x => x.env_id,
                        principalTable: "TestEnvironments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestResults",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    env_id = table.Column<long>(type: "bigint", nullable: false),
                    build = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    result = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    by_id = table.Column<long>(type: "bigint", nullable: false),
                    at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    behalf_of_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResults", x => x.id);
                    table.CheckConstraint("CK_TestResults_Result", "[result] IN (N'PASS', N'FAIL', N'NA', N'UNTEST')");
                    table.ForeignKey(
                        name: "FK_TestResults_BehalfOf",
                        column: x => x.behalf_of_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestResults_By",
                        column: x => x.by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestResults_Case",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestResults_Env",
                        column: x => x.env_id,
                        principalTable: "TestEnvironments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestRetestFlags",
                columns: table => new
                {
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    env_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    bug_issue_id = table.Column<long>(type: "bigint", nullable: true),
                    build = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    req_key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRetestFlags", x => new { x.test_case_id, x.env_id });
                    table.CheckConstraint("CK_TestRetestFlags_Reason", "[reason] IN (N'BUG_FIXED', N'REQ_CHANGED', N'MANUAL')");
                    table.ForeignKey(
                        name: "FK_TestRetestFlags_Bug",
                        column: x => x.bug_issue_id,
                        principalTable: "Issues",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestRetestFlags_Case",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestRetestFlags_Env",
                        column: x => x.env_id,
                        principalTable: "TestEnvironments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestActivities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    plan_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    at = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    actor_id = table.Column<long>(type: "bigint", nullable: true),
                    env_id = table.Column<long>(type: "bigint", nullable: true),
                    module_id = table.Column<long>(type: "bigint", nullable: true),
                    test_case_id = table.Column<long>(type: "bigint", nullable: true),
                    from_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    to_result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    build = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    behalf_of_id = table.Column<long>(type: "bigint", nullable: true),
                    text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestActivities", x => x.id);
                    table.CheckConstraint("CK_TestActivities_Type", "[type] IN (N'RESULT', N'DEPLOY', N'ASSIGN', N'REQ', N'HANDOVER', N'CASE', N'BUG')");
                    table.ForeignKey(
                        name: "FK_TestActivities_Actor",
                        column: x => x.actor_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestActivities_BehalfOf",
                        column: x => x.behalf_of_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestActivities_Case",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestActivities_Env",
                        column: x => x.env_id,
                        principalTable: "TestEnvironments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestActivities_Module",
                        column: x => x.module_id,
                        principalTable: "TestModules",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestActivities_Plan",
                        column: x => x.plan_id,
                        principalTable: "TestPlans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestAssignments",
                columns: table => new
                {
                    module_id = table.Column<long>(type: "bigint", nullable: false),
                    env_id = table.Column<long>(type: "bigint", nullable: false),
                    assignee_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAssignments", x => new { x.module_id, x.env_id });
                    table.ForeignKey(
                        name: "FK_TestAssignments_Assignee",
                        column: x => x.assignee_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestAssignments_Env",
                        column: x => x.env_id,
                        principalTable: "TestEnvironments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestAssignments_Module",
                        column: x => x.module_id,
                        principalTable: "TestModules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 45L,
                column: "sort_order",
                value: 47);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 46L,
                column: "sort_order",
                value: 48);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 47L,
                column: "sort_order",
                value: 49);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 48L,
                column: "sort_order",
                value: 50);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 50L,
                column: "name",
                value: "Chạy Test Case, ghi kết quả (Pass / Fail / N/A) & gắn bug có sẵn");

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[] { 51L, "testcase.plan", null, "6. Kiểm thử & Truy xuất", true, "Thiết lập đợt test (môi trường, module), phân công, regression & bàn giao", "PROJECT", 46 });

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_BugIssue",
                table: "TestCases",
                column: "bug_issue_id",
                filter: "[bug_issue_id] IS NOT NULL AND [status] = N'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_created_by_id",
                table: "TestCases",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_module_id",
                table: "TestCases",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_Plan_Module",
                table: "TestCases",
                columns: new[] { "plan_id", "module_id", "section_id", "position" },
                filter: "[status] = N'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_project_id",
                table: "TestCases",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_section_id",
                table: "TestCases",
                column: "section_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseReqLinks_Req",
                table: "TestCaseRequirementLinks",
                columns: new[] { "document_id", "req_uid" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhases_completed_by_id",
                table: "ProjectPhases",
                column: "completed_by_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPhases_ExitCriteria_Json",
                table: "ProjectPhases",
                sql: "ISJSON([exit_criteria]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectPhases_Lifecycle",
                table: "ProjectPhases",
                sql: "[lifecycle_status] IN (N'UPCOMING', N'ACTIVE', N'COMPLETED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentNodes_DocType",
                table: "DocumentNodes",
                sql: "([kind] = 'FOLDER' AND [doc_type] IS NULL) OR ([kind] = 'PAGE' AND [doc_type] IN ('URD','BRD','SRS','ADR','UI_SPEC','TEST_PLAN','OTHER'))");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseLogs_actor_id",
                table: "ProjectPhaseLogs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseLogs_Phase",
                table: "ProjectPhaseLogs",
                columns: new[] { "phase_id", "at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPhaseLogs_project_id",
                table: "ProjectPhaseLogs",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestActivities_actor_id",
                table: "TestActivities",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestActivities_behalf_of_id",
                table: "TestActivities",
                column: "behalf_of_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestActivities_env_id",
                table: "TestActivities",
                column: "env_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestActivities_module_id",
                table: "TestActivities",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestActivities_Plan_At",
                table: "TestActivities",
                columns: new[] { "plan_id", "at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_TestActivities_test_case_id",
                table: "TestActivities",
                column: "test_case_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_Assignee",
                table: "TestAssignments",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestAssignments_env_id",
                table: "TestAssignments",
                column: "env_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestBuilds_deployed_by_id",
                table: "TestBuilds",
                column: "deployed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestBuilds_Env",
                table: "TestBuilds",
                columns: new[] { "env_id", "deployed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TestEnvironments_Plan",
                table: "TestEnvironments",
                columns: new[] { "plan_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_TestModules_Document",
                table: "TestModules",
                column: "document_id",
                filter: "[document_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TestModules_Plan",
                table: "TestModules",
                columns: new[] { "plan_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_TestPlans_created_by_id",
                table: "TestPlans",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestPlans_handover_by_id",
                table: "TestPlans",
                column: "handover_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestPlans_Project",
                table: "TestPlans",
                columns: new[] { "project_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_behalf_of_id",
                table: "TestResults",
                column: "behalf_of_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_by_id",
                table: "TestResults",
                column: "by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_Case_Env",
                table: "TestResults",
                columns: new[] { "test_case_id", "env_id", "at", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_env_id",
                table: "TestResults",
                column: "env_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestRetestFlags_bug_issue_id",
                table: "TestRetestFlags",
                column: "bug_issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestRetestFlags_env_id",
                table: "TestRetestFlags",
                column: "env_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestSections_Module",
                table: "TestSections",
                columns: new[] { "module_id", "parent_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_TestSections_parent_id",
                table: "TestSections",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPhases_CompletedBy",
                table: "ProjectPhases",
                column: "completed_by_id",
                principalTable: "Users",
                principalColumn: "id");

            migrationBuilder.Sql(@"
;WITH firstActive AS (
  SELECT id, ROW_NUMBER() OVER (PARTITION BY project_id ORDER BY position, id) AS rn
  FROM ProjectPhases WHERE is_active = 1
)
UPDATE p SET lifecycle_status = CASE WHEN f.rn = 1 THEN N'ACTIVE' ELSE N'UPCOMING' END
FROM ProjectPhases p JOIN firstActive f ON f.id = p.id;
");

            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (role_id, permission_id)
SELECT rp.role_id, 51 FROM RolePermissions rp
JOIN CompanyRoles r ON r.id = rp.role_id AND r.scope = 'PROJECT'
WHERE rp.permission_id = 21
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.role_id = rp.role_id AND x.permission_id = 51);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE permission_id = 51;");
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPhases_CompletedBy",
                table: "ProjectPhases");

            migrationBuilder.DropTable(name: "ProjectPhaseLogs");
            migrationBuilder.DropTable(name: "TestActivities");
            migrationBuilder.DropTable(name: "TestAssignments");
            migrationBuilder.DropTable(name: "TestBuilds");
            migrationBuilder.DropTable(name: "TestResults");
            migrationBuilder.DropTable(name: "TestRetestFlags");
            migrationBuilder.DropTable(name: "TestCaseRequirementLinks");
            migrationBuilder.DropTable(name: "TestCases");
            migrationBuilder.DropTable(name: "TestSections");
            migrationBuilder.DropTable(name: "TestEnvironments");
            migrationBuilder.DropTable(name: "TestModules");
            migrationBuilder.DropTable(name: "TestPlans");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPhases_completed_by_id",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPhases_ExitCriteria_Json",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPhases_Lifecycle",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "completed_by_id",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "exit_criteria",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "lifecycle_status",
                table: "ProjectPhases");

            migrationBuilder.DropColumn(
                name: "override_reason",
                table: "ProjectPhases");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 51L);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 45L,
                column: "sort_order",
                value: 46);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 46L,
                column: "sort_order",
                value: 47);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 47L,
                column: "sort_order",
                value: 48);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 48L,
                column: "sort_order",
                value: 49);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 50L,
                column: "name",
                value: "Chạy Test Case & ghi kết quả (Đạt / Lỗi / Chặn)");
        }
    }
}
