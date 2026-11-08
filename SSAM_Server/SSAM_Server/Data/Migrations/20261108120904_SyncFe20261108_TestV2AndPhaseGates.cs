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
            migrationBuilder.DropForeignKey(
                name: "FK_TestCaseRequirementLinks_Document",
                table: "TestCaseRequirementLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCaseRequirementLinks_TestCase",
                table: "TestCaseRequirementLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_Folder",
                table: "TestCases");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_LastRunBy",
                table: "TestCases");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_Projects",
                table: "TestCases");

            migrationBuilder.DropTable(
                name: "TestCaseIssueLinks");

            migrationBuilder.DropTable(
                name: "TestFolders");

            migrationBuilder.DropTable(
                name: "TestRunDefects");

            migrationBuilder.DropTable(
                name: "TestRunHistory");

            migrationBuilder.DropTable(
                name: "TestRuns");

            migrationBuilder.DropTable(
                name: "TestCycles");

            migrationBuilder.Sql("DELETE FROM TestCaseRequirementLinks; DELETE FROM TestCases; DELETE FROM ProjectSequences WHERE scope = 'TESTCASE';");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_folder_id",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_last_run_by_id",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_Project_Folder",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "UQ_TestCases_Project_Key",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_LastResult",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_Priority",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_Type",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentNodes_DocType",
                table: "DocumentNodes");

            migrationBuilder.DropColumn(
                name: "folder_id",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "key",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "last_result",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "last_run_at",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "type",
                table: "TestCases");

            migrationBuilder.RenameColumn(
                name: "step_count",
                table: "TestCases",
                newName: "position");

            migrationBuilder.RenameColumn(
                name: "needs_review",
                table: "TestCases",
                newName: "ai_suggested_reqs");

            migrationBuilder.RenameColumn(
                name: "last_run_by_id",
                table: "TestCases",
                newName: "bug_issue_id");

            migrationBuilder.RenameIndex(
                name: "IX_TestCaseRequirementLinks_Doc_Req",
                table: "TestCaseRequirementLinks",
                newName: "IX_TestCaseReqLinks_Req");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "TestCases",
                type: "datetime2(3)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "TestCases",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "priority",
                table: "TestCases",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "P1",
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldUnicode: false,
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "TestCases",
                type: "datetime2(3)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "actual",
                table: "TestCases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "browser",
                table: "TestCases",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "TestCases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FUNCTIONAL");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "TestCases",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "custom_fields",
                table: "TestCases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<long>(
                name: "module_id",
                table: "TestCases",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "plan_id",
                table: "TestCases",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "section_id",
                table: "TestCases",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "TestCases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AlterColumn<string>(
                name: "req_hash",
                table: "TestCaseRequirementLinks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "char(64)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "linked_at",
                table: "TestCaseRequirementLinks",
                type: "datetime2(3)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "changed_version_label",
                table: "TestCaseRequirementLinks",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldUnicode: false,
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "req_uid",
                table: "TestCaseRequirementLinks",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldUnicode: false,
                oldMaxLength: 30);

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
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_Category",
                table: "TestCases",
                sql: "[category] IN (N'FUNCTIONAL', N'BOUNDARY', N'NEGATIVE', N'UI', N'SECURITY', N'PERFORMANCE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_Custom_Json",
                table: "TestCases",
                sql: "ISJSON([custom_fields]) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_Priority",
                table: "TestCases",
                sql: "[priority] IN (N'P0', N'P1', N'P2', N'P3')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_Status",
                table: "TestCases",
                sql: "[status] IN (N'ACTIVE', N'DELETED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_Steps_Json",
                table: "TestCases",
                sql: "ISJSON([steps]) = 1");

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

            migrationBuilder.AddForeignKey(
                name: "FK_TestCaseReqLinks_Document",
                table: "TestCaseRequirementLinks",
                column: "document_id",
                principalTable: "DocumentNodes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCaseReqLinks_TestCase",
                table: "TestCaseRequirementLinks",
                column: "test_case_id",
                principalTable: "TestCases",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_BugIssue",
                table: "TestCases",
                column: "bug_issue_id",
                principalTable: "Issues",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_Module",
                table: "TestCases",
                column: "module_id",
                principalTable: "TestModules",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_Plan",
                table: "TestCases",
                column: "plan_id",
                principalTable: "TestPlans",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_Project",
                table: "TestCases",
                column: "project_id",
                principalTable: "Projects",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_Section",
                table: "TestCases",
                column: "section_id",
                principalTable: "TestSections",
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

            migrationBuilder.DropForeignKey(
                name: "FK_TestCaseReqLinks_Document",
                table: "TestCaseRequirementLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCaseReqLinks_TestCase",
                table: "TestCaseRequirementLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_BugIssue",
                table: "TestCases");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_Module",
                table: "TestCases");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_Plan",
                table: "TestCases");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_Project",
                table: "TestCases");

            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_Section",
                table: "TestCases");

            migrationBuilder.DropTable(
                name: "ProjectPhaseLogs");

            migrationBuilder.DropTable(
                name: "TestActivities");

            migrationBuilder.DropTable(
                name: "TestAssignments");

            migrationBuilder.DropTable(
                name: "TestBuilds");

            migrationBuilder.DropTable(
                name: "TestResults");

            migrationBuilder.DropTable(
                name: "TestRetestFlags");

            migrationBuilder.DropTable(
                name: "TestSections");

            migrationBuilder.DropTable(
                name: "TestEnvironments");

            migrationBuilder.DropTable(
                name: "TestModules");

            migrationBuilder.DropTable(
                name: "TestPlans");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_BugIssue",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_module_id",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_Plan_Module",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_project_id",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_section_id",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_Category",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_Custom_Json",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_Priority",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_Status",
                table: "TestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestCases_Steps_Json",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_ProjectPhases_completed_by_id",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPhases_ExitCriteria_Json",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectPhases_Lifecycle",
                table: "ProjectPhases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentNodes_DocType",
                table: "DocumentNodes");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 51L);

            migrationBuilder.DropColumn(
                name: "actual",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "browser",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "category",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "code",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "custom_fields",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "module_id",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "plan_id",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "section_id",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "status",
                table: "TestCases");

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

            migrationBuilder.RenameColumn(
                name: "position",
                table: "TestCases",
                newName: "step_count");

            migrationBuilder.RenameColumn(
                name: "bug_issue_id",
                table: "TestCases",
                newName: "last_run_by_id");

            migrationBuilder.RenameColumn(
                name: "ai_suggested_reqs",
                table: "TestCases",
                newName: "needs_review");

            migrationBuilder.RenameIndex(
                name: "IX_TestCaseReqLinks_Req",
                table: "TestCaseRequirementLinks",
                newName: "IX_TestCaseRequirementLinks_Doc_Req");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "TestCases",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2(3)",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "TestCases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "priority",
                table: "TestCases",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldDefaultValue: "P1");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "TestCases",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2(3)",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<long>(
                name: "folder_id",
                table: "TestCases",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "TestCases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "key",
                table: "TestCases",
                type: "varchar(25)",
                unicode: false,
                maxLength: 25,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "last_result",
                table: "TestCases",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_run_at",
                table: "TestCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "TestCases",
                type: "varchar(15)",
                unicode: false,
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "req_hash",
                table: "TestCaseRequirementLinks",
                type: "char(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "linked_at",
                table: "TestCaseRequirementLinks",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2(3)",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<string>(
                name: "changed_version_label",
                table: "TestCaseRequirementLinks",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "req_uid",
                table: "TestCaseRequirementLinks",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateTable(
                name: "TestCaseIssueLinks",
                columns: table => new
                {
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    linked_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseIssueLinks", x => new { x.test_case_id, x.issue_id });
                    table.ForeignKey(
                        name: "FK_TestCaseIssueLinks_Issue",
                        column: x => x.issue_id,
                        principalTable: "Issues",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCaseIssueLinks_TestCase",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCycles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    sprint_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "OPEN"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCycles", x => x.id);
                    table.CheckConstraint("CK_TestCycles_Status", "[status] IN ('OPEN','CLOSED')");
                    table.ForeignKey(
                        name: "FK_TestCycles_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCycles_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestCycles_Sprint",
                        column: x => x.sprint_id,
                        principalTable: "Sprints",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestFolders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestFolders", x => x.id);
                    table.ForeignKey(
                        name: "FK_TestFolders_Parent",
                        column: x => x.parent_id,
                        principalTable: "TestFolders",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestFolders_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestRuns",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    assignee_id = table.Column<long>(type: "bigint", nullable: true),
                    cycle_id = table.Column<long>(type: "bigint", nullable: false),
                    executed_by_id = table.Column<long>(type: "bigint", nullable: true),
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    executed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    needs_rerun = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    result = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "NOT_RUN"),
                    row_ver = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRuns", x => x.id);
                    table.CheckConstraint("CK_TestRuns_Result", "[result] IN ('NOT_RUN','PASS','FAIL','BLOCKED')");
                    table.ForeignKey(
                        name: "FK_TestRuns_Assignee",
                        column: x => x.assignee_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestRuns_Cycle",
                        column: x => x.cycle_id,
                        principalTable: "TestCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestRuns_ExecutedBy",
                        column: x => x.executed_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestRuns_TestCase",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestRunDefects",
                columns: table => new
                {
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRunDefects", x => new { x.run_id, x.issue_id });
                    table.ForeignKey(
                        name: "FK_TestRunDefects_Issue",
                        column: x => x.issue_id,
                        principalTable: "Issues",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestRunDefects_Run",
                        column: x => x.run_id,
                        principalTable: "TestRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestRunHistory",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    defect_issue_id = table.Column<long>(type: "bigint", nullable: true),
                    executed_by_id = table.Column<long>(type: "bigint", nullable: false),
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    executed_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    result = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRunHistory", x => x.id);
                    table.CheckConstraint("CK_TestRunHistory_Result", "[result] IN ('PASS','FAIL','BLOCKED')");
                    table.ForeignKey(
                        name: "FK_TestRunHistory_DefectIssue",
                        column: x => x.defect_issue_id,
                        principalTable: "Issues",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestRunHistory_ExecutedBy",
                        column: x => x.executed_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestRunHistory_Run",
                        column: x => x.run_id,
                        principalTable: "TestRuns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestRunHistory_TestCase",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id");
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_folder_id",
                table: "TestCases",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_last_run_by_id",
                table: "TestCases",
                column: "last_run_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_Project_Folder",
                table: "TestCases",
                columns: new[] { "project_id", "folder_id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "UQ_TestCases_Project_Key",
                table: "TestCases",
                columns: new[] { "project_id", "key" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_LastResult",
                table: "TestCases",
                sql: "[last_result] IS NULL OR [last_result] IN ('NOT_RUN','PASS','FAIL','BLOCKED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_Priority",
                table: "TestCases",
                sql: "[priority] IN ('HIGH','MEDIUM','LOW')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestCases_Type",
                table: "TestCases",
                sql: "[type] IN ('FUNCTIONAL','INTEGRATION','SECURITY','E2E','UNIT','REGRESSION')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentNodes_DocType",
                table: "DocumentNodes",
                sql: "([kind] = 'FOLDER' AND [doc_type] IS NULL) OR ([kind] = 'PAGE' AND [doc_type] IN ('BRD','SRS','ADR','UI_SPEC','TEST_PLAN','OTHER'))");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseIssueLinks_Issue",
                table: "TestCaseIssueLinks",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCycles_created_by_id",
                table: "TestCycles",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCycles_Project_Status",
                table: "TestCycles",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_TestCycles_sprint_id",
                table: "TestCycles",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestFolders_parent_id",
                table: "TestFolders",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestFolders_Project_Parent",
                table: "TestFolders",
                columns: new[] { "project_id", "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_TestRunDefects_Issue",
                table: "TestRunDefects",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestRunHistory_defect_issue_id",
                table: "TestRunHistory",
                column: "defect_issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestRunHistory_executed_by_id",
                table: "TestRunHistory",
                column: "executed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestRunHistory_Run",
                table: "TestRunHistory",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestRunHistory_TestCase_Time",
                table: "TestRunHistory",
                columns: new[] { "test_case_id", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_Assignee",
                table: "TestRuns",
                columns: new[] { "assignee_id", "result" });

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_Cycle_Result",
                table: "TestRuns",
                columns: new[] { "cycle_id", "result" });

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_executed_by_id",
                table: "TestRuns",
                column: "executed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_TestCase",
                table: "TestRuns",
                column: "test_case_id");

            migrationBuilder.CreateIndex(
                name: "UQ_TestRuns_Cycle_TestCase",
                table: "TestRuns",
                columns: new[] { "cycle_id", "test_case_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TestCaseRequirementLinks_Document",
                table: "TestCaseRequirementLinks",
                column: "document_id",
                principalTable: "DocumentNodes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCaseRequirementLinks_TestCase",
                table: "TestCaseRequirementLinks",
                column: "test_case_id",
                principalTable: "TestCases",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_Folder",
                table: "TestCases",
                column: "folder_id",
                principalTable: "TestFolders",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_LastRunBy",
                table: "TestCases",
                column: "last_run_by_id",
                principalTable: "Users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_Projects",
                table: "TestCases",
                column: "project_id",
                principalTable: "Projects",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
