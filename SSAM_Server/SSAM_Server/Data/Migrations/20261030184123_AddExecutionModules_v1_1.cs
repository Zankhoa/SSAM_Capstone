using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionModules_v1_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "CompanyRoles",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "OTHER");

            migrationBuilder.CreateTable(
                name: "BoardWipLimits",
                columns: table => new
                {
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    status_id = table.Column<long>(type: "bigint", nullable: false),
                    wip_limit = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardWipLimits", x => new { x.project_id, x.status_id });
                    table.CheckConstraint("CK_BoardWipLimits_Limit", "[wip_limit] >= 1");
                    table.ForeignKey(
                        name: "FK_BoardWipLimits_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardWipLimits_Status",
                        column: x => x.status_id,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectSequences",
                columns: table => new
                {
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    scope = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    last_value = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSequences", x => new { x.project_id, x.scope });
                    table.CheckConstraint("CK_ProjectSequences_Scope", "[scope] IN ('ISSUE','TESTCASE','REQ','DOC_BRD','DOC_SRS','DOC_ADR','DOC_UI_SPEC','DOC_TEST_PLAN','DOC_OTHER')");
                    table.CheckConstraint("CK_ProjectSequences_Value", "[last_value] >= 0");
                    table.ForeignKey(
                        name: "FK_ProjectSequences_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sprints",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    goal = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    capacity_points = table.Column<int>(type: "int", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.id);
                    table.CheckConstraint("CK_Sprints_Capacity", "[capacity_points] IS NULL OR [capacity_points] >= 0");
                    table.CheckConstraint("CK_Sprints_Dates", "[end_date] IS NULL OR [start_date] IS NULL OR [end_date] >= [start_date]");
                    table.CheckConstraint("CK_Sprints_Status", "[status] IN ('PLANNED','ACTIVE','COMPLETED')");
                    table.ForeignKey(
                        name: "FK_Sprints_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Sprints_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestFolders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
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
                name: "Issues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: false),
                    type = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    status_id = table.Column<long>(type: "bigint", nullable: false),
                    priority = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    points = table.Column<int>(type: "int", nullable: true),
                    assignee_id = table.Column<long>(type: "bigint", nullable: true),
                    reporter_id = table.Column<long>(type: "bigint", nullable: false),
                    sprint_id = table.Column<long>(type: "bigint", nullable: true),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    rank = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_BIN2"),
                    labels = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: "[]"),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    req_changed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    row_ver = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.id);
                    table.CheckConstraint("CK_Issues_Points", "[points] IS NULL OR ([points] >= 0 AND [points] <= 100)");
                    table.CheckConstraint("CK_Issues_Priority", "[priority] IN ('HIGHEST','HIGH','MEDIUM','LOW')");
                    table.CheckConstraint("CK_Issues_Type", "[type] IN ('TASK','BUG','STORY','DESIGN')");
                    table.ForeignKey(
                        name: "FK_Issues_Assignee",
                        column: x => x.assignee_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Issues_Parent",
                        column: x => x.parent_id,
                        principalTable: "Issues",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Issues_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Issues_Reporter",
                        column: x => x.reporter_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Issues_Sprint",
                        column: x => x.sprint_id,
                        principalTable: "Sprints",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Issues_Status",
                        column: x => x.status_id,
                        principalTable: "WorkflowStatuses",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestCycles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    sprint_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "OPEN"),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
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
                name: "TestCases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    priority = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    preconditions = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    steps = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    step_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    folder_id = table.Column<long>(type: "bigint", nullable: true),
                    needs_review = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    last_result = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    last_run_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_run_by_id = table.Column<long>(type: "bigint", nullable: true),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.id);
                    table.CheckConstraint("CK_TestCases_LastResult", "[last_result] IS NULL OR [last_result] IN ('NOT_RUN','PASS','FAIL','BLOCKED')");
                    table.CheckConstraint("CK_TestCases_Priority", "[priority] IN ('HIGH','MEDIUM','LOW')");
                    table.CheckConstraint("CK_TestCases_Type", "[type] IN ('FUNCTIONAL','INTEGRATION','SECURITY','E2E','UNIT','REGRESSION')");
                    table.ForeignKey(
                        name: "FK_TestCases_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_Folder",
                        column: x => x.folder_id,
                        principalTable: "TestFolders",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_LastRunBy",
                        column: x => x.last_run_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCases_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueActivities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: false),
                    actor_id = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueActivities", x => x.id);
                    table.CheckConstraint("CK_IssueActivities_Action", "[action] IN ('CREATED','UPDATED','TRANSITIONED','ASSIGNED','SPRINT_CHANGED','REQ_LINKED','REQ_ACKED','REQ_CHANGED','COMMENTED','DELETED')");
                    table.ForeignKey(
                        name: "FK_IssueActivities_Actor",
                        column: x => x.actor_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_IssueActivities_Issue",
                        column: x => x.issue_id,
                        principalTable: "Issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IssueComments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueComments", x => x.id);
                    table.ForeignKey(
                        name: "FK_IssueComments_Author",
                        column: x => x.author_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_IssueComments_Issue",
                        column: x => x.issue_id,
                        principalTable: "Issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueComments_Parent",
                        column: x => x.parent_id,
                        principalTable: "IssueComments",
                        principalColumn: "id");
                });

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
                name: "TestRuns",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    cycle_id = table.Column<long>(type: "bigint", nullable: false),
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    assignee_id = table.Column<long>(type: "bigint", nullable: true),
                    result = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "NOT_RUN"),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    executed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    executed_by_id = table.Column<long>(type: "bigint", nullable: true),
                    needs_rerun = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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
                name: "IssueCommentMentions",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueCommentMentions", x => new { x.comment_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_IssueCommentMentions_Comment",
                        column: x => x.comment_id,
                        principalTable: "IssueComments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueCommentMentions_User",
                        column: x => x.user_id,
                        principalTable: "Users",
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
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    result = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    defect_issue_id = table.Column<long>(type: "bigint", nullable: true),
                    executed_by_id = table.Column<long>(type: "bigint", nullable: false),
                    executed_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
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

            migrationBuilder.CreateTable(
                name: "DocumentActivities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    version_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    actor_id = table.Column<long>(type: "bigint", nullable: false),
                    note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentActivities", x => x.id);
                    table.CheckConstraint("CK_DocumentActivities_Action", "[action] IN ('CREATED','DELETED','GENERATED_FROM_NOTES','CONTENT_SAVED','SUBMITTED','APPROVED','CHANGES_REQUESTED','REVISION_CREATED','RENAMED','MOVED','REQ_CONFIRMED_FRESH','COMMENTED')");
                    table.ForeignKey(
                        name: "FK_DocumentActivities_Actor",
                        column: x => x.actor_id,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "DocumentCommentMentions",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCommentMentions", x => new { x.comment_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_DocumentCommentMentions_User",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "DocumentComments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    anchor_id = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: true),
                    quote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    resolved_by_id = table.Column<long>(type: "bigint", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentComments", x => x.id);
                    table.ForeignKey(
                        name: "FK_DocumentComments_Author",
                        column: x => x.author_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentComments_Parent",
                        column: x => x.parent_id,
                        principalTable: "DocumentComments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentComments_ResolvedBy",
                        column: x => x.resolved_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "DocumentGenerations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: true),
                    raw_notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    fields = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    items = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGenerations", x => x.id);
                    table.ForeignKey(
                        name: "FK_DocumentGenerations_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentGenerations_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentNodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    kind = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    doc_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    code = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    current_version_id = table.Column<long>(type: "bigint", nullable: true),
                    baseline_version_id = table.Column<long>(type: "bigint", nullable: true),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentNodes", x => x.id);
                    table.CheckConstraint("CK_DocumentNodes_DocType", "([kind] = 'FOLDER' AND [doc_type] IS NULL) OR ([kind] = 'PAGE' AND [doc_type] IN ('BRD','SRS','ADR','UI_SPEC','TEST_PLAN','OTHER'))");
                    table.CheckConstraint("CK_DocumentNodes_Kind", "[kind] IN ('FOLDER','PAGE')");
                    table.ForeignKey(
                        name: "FK_DocumentNodes_CreatedBy",
                        column: x => x.created_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentNodes_Parent",
                        column: x => x.parent_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentNodes_Projects",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    label = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    subtitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    content_text = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    base_version_id = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    updated_by_id = table.Column<long>(type: "bigint", nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    submitted_by_id = table.Column<long>(type: "bigint", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reviewed_by_id = table.Column<long>(type: "bigint", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    review_note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.id);
                    table.CheckConstraint("CK_DocumentVersions_Status", "[status] IN ('DRAFT','IN_REVIEW','CHANGES_REQUESTED','APPROVED','SUPERSEDED')");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Author",
                        column: x => x.author_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_BaseVersion",
                        column: x => x.base_version_id,
                        principalTable: "DocumentVersions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Document",
                        column: x => x.document_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_ReviewedBy",
                        column: x => x.reviewed_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_SubmittedBy",
                        column: x => x.submitted_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_DocumentVersions_UpdatedBy",
                        column: x => x.updated_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "IssueRequirementLinks",
                columns: table => new
                {
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    req_uid = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    req_hash = table.Column<string>(type: "char(64)", nullable: false),
                    changed_version_label = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    linked_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    linked_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueRequirementLinks", x => new { x.issue_id, x.document_id, x.req_uid });
                    table.ForeignKey(
                        name: "FK_IssueRequirementLinks_Document",
                        column: x => x.document_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_IssueRequirementLinks_Issue",
                        column: x => x.issue_id,
                        principalTable: "Issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueRequirementLinks_LinkedBy",
                        column: x => x.linked_by_id,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "TestCaseRequirementLinks",
                columns: table => new
                {
                    test_case_id = table.Column<long>(type: "bigint", nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    req_uid = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    req_hash = table.Column<string>(type: "char(64)", nullable: false),
                    changed_version_label = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    linked_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseRequirementLinks", x => new { x.test_case_id, x.document_id, x.req_uid });
                    table.ForeignKey(
                        name: "FK_TestCaseRequirementLinks_Document",
                        column: x => x.document_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_TestCaseRequirementLinks_TestCase",
                        column: x => x.test_case_id,
                        principalTable: "TestCases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementItems",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    req_uid = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    anchor_id = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    body_preview = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, defaultValue: ""),
                    body_length = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    acceptance_criteria = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    has_table = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    has_diagram = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    has_figma = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    content_hash = table.Column<string>(type: "char(64)", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementItems", x => x.id);
                    table.ForeignKey(
                        name: "FK_RequirementItems_Document",
                        column: x => x.document_id,
                        principalTable: "DocumentNodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_RequirementItems_Version",
                        column: x => x.version_id,
                        principalTable: "DocumentVersions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 35L,
                column: "sort_order",
                value: 35);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 36L,
                column: "sort_order",
                value: 36);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 37L,
                column: "sort_order",
                value: 37);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 38L,
                column: "sort_order",
                value: 38);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 39L,
                column: "sort_order",
                value: 39);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 40L,
                column: "sort_order",
                value: 40);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 41L,
                column: "sort_order",
                value: 41);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 42L,
                column: "sort_order",
                value: 42);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 43L,
                column: "sort_order",
                value: 43);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 44L,
                column: "sort_order",
                value: 44);

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

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 49L, "sprint.manage", null, "3. Quản lý Issue & Công việc", true, "Tạo, bắt đầu & hoàn tất Sprint", "PROJECT", 34 },
                    { 50L, "testcase.execute", null, "6. Kiểm thử & Truy xuất", true, "Chạy Test Case & ghi kết quả (Đạt / Lỗi / Chặn)", "PROJECT", 45 }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompanyRoles_Kind",
                table: "CompanyRoles",
                sql: "[kind] IN ('BA', 'DESIGNER', 'QA', 'DEV', 'PM', 'OTHER')");

            migrationBuilder.CreateIndex(
                name: "IX_BoardWipLimits_status_id",
                table: "BoardWipLimits",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_actor_id",
                table: "DocumentActivities",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_Doc_Time",
                table: "DocumentActivities",
                columns: new[] { "document_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentActivities_version_id",
                table: "DocumentActivities",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCommentMentions_User",
                table: "DocumentCommentMentions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComments_author_id",
                table: "DocumentComments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComments_Doc_Version",
                table: "DocumentComments",
                columns: new[] { "document_id", "version_id", "resolved_at" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComments_Parent",
                table: "DocumentComments",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComments_resolved_by_id",
                table: "DocumentComments",
                column: "resolved_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentComments_version_id",
                table: "DocumentComments",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGenerations_created_by_id",
                table: "DocumentGenerations",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGenerations_project_id",
                table: "DocumentGenerations",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "UQ_DocumentGenerations_Document",
                table: "DocumentGenerations",
                column: "document_id",
                unique: true,
                filter: "[document_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNodes_baseline_version_id",
                table: "DocumentNodes",
                column: "baseline_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNodes_created_by_id",
                table: "DocumentNodes",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNodes_current_version_id",
                table: "DocumentNodes",
                column: "current_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNodes_parent_id",
                table: "DocumentNodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNodes_Project_Parent",
                table: "DocumentNodes",
                columns: new[] { "project_id", "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "UQ_DocumentNodes_Project_Code",
                table: "DocumentNodes",
                columns: new[] { "project_id", "code" },
                unique: true,
                filter: "[code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_author_id",
                table: "DocumentVersions",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_base_version_id",
                table: "DocumentVersions",
                column: "base_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_Doc_Status",
                table: "DocumentVersions",
                columns: new[] { "document_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_reviewed_by_id",
                table: "DocumentVersions",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_submitted_by_id",
                table: "DocumentVersions",
                column: "submitted_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_updated_by_id",
                table: "DocumentVersions",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "UQ_DocumentVersions_Doc_Label",
                table: "DocumentVersions",
                columns: new[] { "document_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueActivities_actor_id",
                table: "IssueActivities",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_IssueActivities_Issue_Time",
                table: "IssueActivities",
                columns: new[] { "issue_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueCommentMentions_User",
                table: "IssueCommentMentions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_author_id",
                table: "IssueComments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_Issue_Time",
                table: "IssueComments",
                columns: new[] { "issue_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_Parent",
                table: "IssueComments",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_IssueRequirementLinks_Doc_Req",
                table: "IssueRequirementLinks",
                columns: new[] { "document_id", "req_uid" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueRequirementLinks_linked_by_id",
                table: "IssueRequirementLinks",
                column: "linked_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_assignee_id",
                table: "Issues",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Parent",
                table: "Issues",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Project_Assignee",
                table: "Issues",
                columns: new[] { "project_id", "assignee_id", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_Project_Sprint_Status_Rank",
                table: "Issues",
                columns: new[] { "project_id", "sprint_id", "status_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_reporter_id",
                table: "Issues",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_sprint_id",
                table: "Issues",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_status_id",
                table: "Issues",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "UQ_Issues_Project_Key",
                table: "Issues",
                columns: new[] { "project_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequirementItems_Doc_Uid",
                table: "RequirementItems",
                columns: new[] { "document_id", "req_uid" });

            migrationBuilder.CreateIndex(
                name: "UQ_RequirementItems_Version_Uid",
                table: "RequirementItems",
                columns: new[] { "version_id", "req_uid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_created_by_id",
                table: "Sprints",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_Project_Status",
                table: "Sprints",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "UQ_Sprints_OneActive",
                table: "Sprints",
                column: "project_id",
                unique: true,
                filter: "[status] = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseIssueLinks_Issue",
                table: "TestCaseIssueLinks",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRequirementLinks_Doc_Req",
                table: "TestCaseRequirementLinks",
                columns: new[] { "document_id", "req_uid" });

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_created_by_id",
                table: "TestCases",
                column: "created_by_id");

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
                name: "FK_DocumentActivities_Document",
                table: "DocumentActivities",
                column: "document_id",
                principalTable: "DocumentNodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentActivities_Version",
                table: "DocumentActivities",
                column: "version_id",
                principalTable: "DocumentVersions",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentCommentMentions_Comment",
                table: "DocumentCommentMentions",
                column: "comment_id",
                principalTable: "DocumentComments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentComments_Document",
                table: "DocumentComments",
                column: "document_id",
                principalTable: "DocumentNodes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentComments_Version",
                table: "DocumentComments",
                column: "version_id",
                principalTable: "DocumentVersions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentGenerations_Document",
                table: "DocumentGenerations",
                column: "document_id",
                principalTable: "DocumentNodes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentNodes_BaselineVersion",
                table: "DocumentNodes",
                column: "baseline_version_id",
                principalTable: "DocumentVersions",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentNodes_CurrentVersion",
                table: "DocumentNodes",
                column: "current_version_id",
                principalTable: "DocumentVersions",
                principalColumn: "id");

            // S2 & S3: Update CompanyRoles.kind for existing system roles
            migrationBuilder.Sql(@"
UPDATE CompanyRoles SET kind = 'PM'       WHERE scope = 'PROJECT' AND code IN ('PROJECT_MANAGER', 'PM');
UPDATE CompanyRoles SET kind = 'BA'       WHERE scope = 'PROJECT' AND code = 'BA';
UPDATE CompanyRoles SET kind = 'DEV'      WHERE scope = 'PROJECT' AND code IN ('DEVELOPER', 'DEV', 'TECH_LEAD');
UPDATE CompanyRoles SET kind = 'QA'       WHERE scope = 'PROJECT' AND code = 'QA';
UPDATE CompanyRoles SET kind = 'DESIGNER' WHERE scope = 'PROJECT' AND code = 'DESIGNER';
");

            // S4: Seed DESIGNER system role for existing companies
            migrationBuilder.Sql(@"
INSERT INTO CompanyRoles (company_id, name, code, scope, kind, description, is_system, is_active, created_at, updated_at)
SELECT c.id, N'Designer', 'DESIGNER', 'PROJECT', 'DESIGNER', N'Thiết kế UI/UX', 1, 1, GETUTCDATE(), GETUTCDATE()
FROM Companies c
WHERE NOT EXISTS (
    SELECT 1 FROM CompanyRoles r WHERE r.company_id = c.id AND r.code = 'DESIGNER'
);
");

            // S5: Sync default RolePermissions for system PROJECT roles
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (role_id, permission_id)
SELECT r.id, p.id
FROM CompanyRoles r
JOIN Permissions p ON p.scope = 'PROJECT'
WHERE r.is_system = 1 AND r.scope = 'PROJECT' AND r.code IN ('PROJECT_MANAGER', 'PM')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id);

INSERT INTO RolePermissions (role_id, permission_id)
SELECT r.id, p.id
FROM CompanyRoles r
JOIN Permissions p ON p.id IN (49, 50)
WHERE r.is_system = 1 AND r.scope = 'PROJECT' AND r.code = 'TECH_LEAD'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id);

INSERT INTO RolePermissions (role_id, permission_id)
SELECT r.id, p.id
FROM CompanyRoles r
JOIN Permissions p ON p.id IN (50)
WHERE r.is_system = 1 AND r.scope = 'PROJECT' AND r.code = 'QA'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id);

INSERT INTO RolePermissions (role_id, permission_id)
SELECT r.id, p.id
FROM CompanyRoles r
JOIN Permissions p ON p.id IN (16, 27, 29, 30, 31, 33, 35, 36, 38, 40, 41, 42, 43, 45, 46, 47)
WHERE r.is_system = 1 AND r.scope = 'PROJECT' AND r.code = 'DESIGNER'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentVersions_Document",
                table: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "BoardWipLimits");

            migrationBuilder.DropTable(
                name: "DocumentActivities");

            migrationBuilder.DropTable(
                name: "DocumentCommentMentions");

            migrationBuilder.DropTable(
                name: "DocumentGenerations");

            migrationBuilder.DropTable(
                name: "IssueActivities");

            migrationBuilder.DropTable(
                name: "IssueCommentMentions");

            migrationBuilder.DropTable(
                name: "IssueRequirementLinks");

            migrationBuilder.DropTable(
                name: "ProjectSequences");

            migrationBuilder.DropTable(
                name: "RequirementItems");

            migrationBuilder.DropTable(
                name: "TestCaseIssueLinks");

            migrationBuilder.DropTable(
                name: "TestCaseRequirementLinks");

            migrationBuilder.DropTable(
                name: "TestRunDefects");

            migrationBuilder.DropTable(
                name: "TestRunHistory");

            migrationBuilder.DropTable(
                name: "DocumentComments");

            migrationBuilder.DropTable(
                name: "IssueComments");

            migrationBuilder.DropTable(
                name: "TestRuns");

            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "TestCycles");

            migrationBuilder.DropTable(
                name: "TestCases");

            migrationBuilder.DropTable(
                name: "Sprints");

            migrationBuilder.DropTable(
                name: "TestFolders");

            migrationBuilder.DropTable(
                name: "DocumentNodes");

            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompanyRoles_Kind",
                table: "CompanyRoles");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 49L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 50L);

            migrationBuilder.DropColumn(
                name: "kind",
                table: "CompanyRoles");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 35L,
                column: "sort_order",
                value: 34);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 36L,
                column: "sort_order",
                value: 35);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 37L,
                column: "sort_order",
                value: 36);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 38L,
                column: "sort_order",
                value: 37);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 39L,
                column: "sort_order",
                value: 38);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 40L,
                column: "sort_order",
                value: 39);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 41L,
                column: "sort_order",
                value: 40);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 42L,
                column: "sort_order",
                value: 41);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 43L,
                column: "sort_order",
                value: 42);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 44L,
                column: "sort_order",
                value: 43);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 45L,
                column: "sort_order",
                value: 44);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 46L,
                column: "sort_order",
                value: 45);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 47L,
                column: "sort_order",
                value: 46);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 48L,
                column: "sort_order",
                value: 47);
        }
    }
}
