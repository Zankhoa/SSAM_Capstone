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
            // Batch 1: Drop legacy Test v1 tables & update DocumentNodes doc_type check constraint
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS [TestCaseIssueLinks];
DROP TABLE IF EXISTS [TestCaseRequirementLinks];
DROP TABLE IF EXISTS [TestRunDefects];
DROP TABLE IF EXISTS [TestRunHistory];
DROP TABLE IF EXISTS [TestRuns];
DROP TABLE IF EXISTS [TestCycles];
DROP TABLE IF EXISTS [TestCases];
DROP TABLE IF EXISTS [TestFolders];

DELETE FROM [ProjectSequences] WHERE [scope] = 'TESTCASE';

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DocumentNodes_DocType')
    ALTER TABLE [DocumentNodes] DROP CONSTRAINT [CK_DocumentNodes_DocType];

ALTER TABLE [DocumentNodes] WITH CHECK ADD CONSTRAINT [CK_DocumentNodes_DocType]
    CHECK (([kind] = 'FOLDER' AND [doc_type] IS NULL) OR ([kind] = 'PAGE' AND [doc_type] IN ('URD','BRD','SRS','ADR','UI_SPEC','TEST_PLAN','OTHER')));
");

            // Batch 2: Add new columns to ProjectPhases (must be in its own batch before referencing them in constraints/indexes/updates)
            migrationBuilder.Sql(@"
IF COL_LENGTH('ProjectPhases', 'completed_at') IS NULL
    ALTER TABLE [ProjectPhases] ADD [completed_at] datetime2(3) NULL;

IF COL_LENGTH('ProjectPhases', 'completed_by_id') IS NULL
    ALTER TABLE [ProjectPhases] ADD [completed_by_id] bigint NULL;

IF COL_LENGTH('ProjectPhases', 'exit_criteria') IS NULL
    ALTER TABLE [ProjectPhases] ADD [exit_criteria] nvarchar(max) NOT NULL CONSTRAINT [DF_ProjectPhases_ExitCriteria] DEFAULT N'[]';

IF COL_LENGTH('ProjectPhases', 'lifecycle_status') IS NULL
    ALTER TABLE [ProjectPhases] ADD [lifecycle_status] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectPhases_LifecycleStatus] DEFAULT N'UPCOMING';

IF COL_LENGTH('ProjectPhases', 'override_reason') IS NULL
    ALTER TABLE [ProjectPhases] ADD [override_reason] nvarchar(500) NULL;
");

            // Batch 3: Constraints & indexes on the newly added ProjectPhases columns + ProjectPhaseLogs table
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProjectPhases_completed_by_id' AND object_id = OBJECT_ID('ProjectPhases'))
    EXEC(N'CREATE INDEX [IX_ProjectPhases_completed_by_id] ON [ProjectPhases] ([completed_by_id])');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProjectPhases_CompletedBy')
    EXEC(N'ALTER TABLE [ProjectPhases] ADD CONSTRAINT [FK_ProjectPhases_CompletedBy] FOREIGN KEY ([completed_by_id]) REFERENCES [Users] ([id])');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ProjectPhases_ExitCriteria_Json')
    EXEC(N'ALTER TABLE [ProjectPhases] ADD CONSTRAINT [CK_ProjectPhases_ExitCriteria_Json] CHECK (ISJSON([exit_criteria]) = 1)');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ProjectPhases_Lifecycle')
    EXEC(N'ALTER TABLE [ProjectPhases] ADD CONSTRAINT [CK_ProjectPhases_Lifecycle] CHECK ([lifecycle_status] IN (N''UPCOMING'', N''ACTIVE'', N''COMPLETED''))');

IF OBJECT_ID('ProjectPhaseLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [ProjectPhaseLogs] (
        [id] bigint NOT NULL IDENTITY(1,1),
        [project_id] bigint NOT NULL,
        [phase_id] bigint NOT NULL,
        [action] nvarchar(30) NOT NULL,
        [actor_id] bigint NOT NULL,
        [at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [unmet_criteria] nvarchar(max) NULL,
        [reason] nvarchar(500) NULL,
        CONSTRAINT [PK_ProjectPhaseLogs] PRIMARY KEY ([id]),
        CONSTRAINT [CK_ProjectPhaseLogs_Action] CHECK ([action] IN (N'COMPLETE', N'OVERRIDE_COMPLETE', N'REOPEN')),
        CONSTRAINT [FK_ProjectPhaseLogs_Actor] FOREIGN KEY ([actor_id]) REFERENCES [Users] ([id]),
        CONSTRAINT [FK_ProjectPhaseLogs_Phase] FOREIGN KEY ([phase_id]) REFERENCES [ProjectPhases] ([id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectPhaseLogs_Project] FOREIGN KEY ([project_id]) REFERENCES [Projects] ([id])
    );
    CREATE INDEX [IX_ProjectPhaseLogs_actor_id] ON [ProjectPhaseLogs] ([actor_id]);
    CREATE INDEX [IX_ProjectPhaseLogs_Phase] ON [ProjectPhaseLogs] ([phase_id], [at] DESC);
    CREATE INDEX [IX_ProjectPhaseLogs_project_id] ON [ProjectPhaseLogs] ([project_id]);
END;
");

            // Batch 4: Create Test v2 tables
            migrationBuilder.Sql(@"
CREATE TABLE [TestPlans] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [project_id] bigint NOT NULL,
    [name] nvarchar(200) NOT NULL,
    [start_date] date NOT NULL,
    [end_date] date NOT NULL,
    [status] nvarchar(20) NOT NULL DEFAULT N'OPEN',
    [handover_by_id] bigint NULL,
    [handover_at] datetime2(3) NULL,
    [handover_note] nvarchar(1000) NULL,
    [sheet_layout] nvarchar(max) NULL,
    [created_by_id] bigint NOT NULL,
    [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_TestPlans] PRIMARY KEY ([id]),
    CONSTRAINT [CK_TestPlans_Layout_Json] CHECK ([sheet_layout] IS NULL OR ISJSON([sheet_layout]) = 1),
    CONSTRAINT [CK_TestPlans_Status] CHECK ([status] IN (N'OPEN', N'HANDED_OVER')),
    CONSTRAINT [FK_TestPlans_CreatedBy] FOREIGN KEY ([created_by_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestPlans_HandoverBy] FOREIGN KEY ([handover_by_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestPlans_Project] FOREIGN KEY ([project_id]) REFERENCES [Projects] ([id])
);
CREATE INDEX [IX_TestPlans_created_by_id] ON [TestPlans] ([created_by_id]);
CREATE INDEX [IX_TestPlans_handover_by_id] ON [TestPlans] ([handover_by_id]);
CREATE INDEX [IX_TestPlans_Project] ON [TestPlans] ([project_id], [created_at] DESC);

CREATE TABLE [TestEnvironments] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [plan_id] bigint NOT NULL,
    [name] nvarchar(100) NOT NULL,
    [url] nvarchar(500) NOT NULL DEFAULT N'',
    [note] nvarchar(300) NOT NULL DEFAULT N'',
    [position] int NOT NULL DEFAULT 0,
    [is_deleted] bit NOT NULL DEFAULT 0,
    [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_TestEnvironments] PRIMARY KEY ([id]),
    CONSTRAINT [FK_TestEnvironments_Plan] FOREIGN KEY ([plan_id]) REFERENCES [TestPlans] ([id])
);
CREATE INDEX [IX_TestEnvironments_Plan] ON [TestEnvironments] ([plan_id], [position]);

CREATE TABLE [TestModules] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [plan_id] bigint NOT NULL,
    [document_id] bigint NULL,
    [name] nvarchar(200) NOT NULL,
    [short_code] nvarchar(20) NOT NULL,
    [position] int NOT NULL DEFAULT 0,
    [case_seq] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_TestModules] PRIMARY KEY ([id]),
    CONSTRAINT [FK_TestModules_Document] FOREIGN KEY ([document_id]) REFERENCES [DocumentNodes] ([id]),
    CONSTRAINT [FK_TestModules_Plan] FOREIGN KEY ([plan_id]) REFERENCES [TestPlans] ([id])
);
CREATE INDEX [IX_TestModules_Document] ON [TestModules] ([document_id]) WHERE [document_id] IS NOT NULL;
CREATE INDEX [IX_TestModules_Plan] ON [TestModules] ([plan_id], [position]);

CREATE TABLE [TestSections] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [module_id] bigint NOT NULL,
    [parent_id] bigint NULL,
    [code] nvarchar(20) NOT NULL,
    [title] nvarchar(300) NOT NULL,
    [anchor_id] nvarchar(100) NULL,
    [position] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_TestSections] PRIMARY KEY ([id]),
    CONSTRAINT [FK_TestSections_Module] FOREIGN KEY ([module_id]) REFERENCES [TestModules] ([id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TestSections_Parent] FOREIGN KEY ([parent_id]) REFERENCES [TestSections] ([id])
);
CREATE INDEX [IX_TestSections_Module] ON [TestSections] ([module_id], [parent_id], [position]);
CREATE INDEX [IX_TestSections_parent_id] ON [TestSections] ([parent_id]);

CREATE TABLE [TestCases] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [project_id] bigint NOT NULL,
    [plan_id] bigint NOT NULL,
    [module_id] bigint NOT NULL,
    [section_id] bigint NOT NULL,
    [code] nvarchar(30) NOT NULL,
    [title] nvarchar(500) NOT NULL DEFAULT N'',
    [preconditions] nvarchar(max) NOT NULL DEFAULT N'',
    [steps] nvarchar(max) NOT NULL DEFAULT N'[]',
    [actual] nvarchar(max) NOT NULL DEFAULT N'',
    [ai_suggested_reqs] bit NOT NULL DEFAULT 0,
    [priority] nvarchar(10) NOT NULL DEFAULT N'MEDIUM',
    [category] nvarchar(100) NOT NULL DEFAULT N'',
    [browser] nvarchar(100) NOT NULL DEFAULT N'',
    [custom_fields] nvarchar(max) NOT NULL DEFAULT N'{}',
    [status] nvarchar(20) NOT NULL DEFAULT N'ACTIVE',
    [position] int NOT NULL DEFAULT 0,
    [bug_issue_id] bigint NULL,
    [created_by_id] bigint NOT NULL,
    [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_TestCases] PRIMARY KEY ([id]),
    CONSTRAINT [CK_TestCases_Custom_Json] CHECK (ISJSON([custom_fields]) = 1),
    CONSTRAINT [CK_TestCases_Priority] CHECK ([priority] IN (N'HIGH', N'MEDIUM', N'LOW', N'P0', N'P1', N'P2', N'P3')),
    CONSTRAINT [CK_TestCases_Status] CHECK ([status] IN (N'ACTIVE', N'DELETED')),
    CONSTRAINT [CK_TestCases_Steps_Json] CHECK (ISJSON([steps]) = 1),
    CONSTRAINT [FK_TestCases_BugIssue] FOREIGN KEY ([bug_issue_id]) REFERENCES [Issues] ([id]),
    CONSTRAINT [FK_TestCases_CreatedBy] FOREIGN KEY ([created_by_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestCases_Module] FOREIGN KEY ([module_id]) REFERENCES [TestModules] ([id]),
    CONSTRAINT [FK_TestCases_Plan] FOREIGN KEY ([plan_id]) REFERENCES [TestPlans] ([id]),
    CONSTRAINT [FK_TestCases_Project] FOREIGN KEY ([project_id]) REFERENCES [Projects] ([id]),
    CONSTRAINT [FK_TestCases_Section] FOREIGN KEY ([section_id]) REFERENCES [TestSections] ([id])
);
CREATE INDEX [IX_TestCases_BugIssue] ON [TestCases] ([bug_issue_id]) WHERE [bug_issue_id] IS NOT NULL AND [status] = N'ACTIVE';
CREATE INDEX [IX_TestCases_created_by_id] ON [TestCases] ([created_by_id]);
CREATE INDEX [IX_TestCases_module_id] ON [TestCases] ([module_id]);
CREATE INDEX [IX_TestCases_Plan_Module] ON [TestCases] ([plan_id], [module_id], [section_id], [position]) WHERE [status] = N'ACTIVE';
CREATE INDEX [IX_TestCases_project_id] ON [TestCases] ([project_id]);
CREATE INDEX [IX_TestCases_section_id] ON [TestCases] ([section_id]);

CREATE TABLE [TestCaseRequirementLinks] (
    [test_case_id] bigint NOT NULL,
    [document_id] bigint NOT NULL,
    [req_uid] nvarchar(40) NOT NULL,
    [req_hash] nvarchar(32) NOT NULL DEFAULT N'',
    [changed_version_label] nvarchar(30) NULL,
    [linked_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_TestCaseRequirementLinks] PRIMARY KEY ([test_case_id], [document_id], [req_uid]),
    CONSTRAINT [FK_TestCaseReqLinks_Document] FOREIGN KEY ([document_id]) REFERENCES [DocumentNodes] ([id]),
    CONSTRAINT [FK_TestCaseReqLinks_TestCase] FOREIGN KEY ([test_case_id]) REFERENCES [TestCases] ([id]) ON DELETE CASCADE
);
CREATE INDEX [IX_TestCaseReqLinks_Req] ON [TestCaseRequirementLinks] ([document_id], [req_uid]);

CREATE TABLE [TestBuilds] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [env_id] bigint NOT NULL,
    [version] nvarchar(80) NOT NULL,
    [deployed_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [deployed_by_id] bigint NOT NULL,
    [fixed_issue_ids] nvarchar(max) NOT NULL DEFAULT N'[]',
    CONSTRAINT [PK_TestBuilds] PRIMARY KEY ([id]),
    CONSTRAINT [CK_TestBuilds_FixedIssues_Json] CHECK (ISJSON([fixed_issue_ids]) = 1),
    CONSTRAINT [FK_TestBuilds_DeployedBy] FOREIGN KEY ([deployed_by_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestBuilds_Env] FOREIGN KEY ([env_id]) REFERENCES [TestEnvironments] ([id]) ON DELETE CASCADE
);
CREATE INDEX [IX_TestBuilds_deployed_by_id] ON [TestBuilds] ([deployed_by_id]);
CREATE INDEX [IX_TestBuilds_Env] ON [TestBuilds] ([env_id], [deployed_at] DESC);

CREATE TABLE [TestResults] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [test_case_id] bigint NOT NULL,
    [env_id] bigint NOT NULL,
    [build] nvarchar(80) NOT NULL,
    [result] nvarchar(10) NOT NULL,
    [by_id] bigint NOT NULL,
    [at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [behalf_of_id] bigint NULL,
    CONSTRAINT [PK_TestResults] PRIMARY KEY ([id]),
    CONSTRAINT [CK_TestResults_Result] CHECK ([result] IN (N'PASS', N'FAIL', N'NA', N'UNTEST')),
    CONSTRAINT [FK_TestResults_BehalfOf] FOREIGN KEY ([behalf_of_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestResults_By] FOREIGN KEY ([by_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestResults_Case] FOREIGN KEY ([test_case_id]) REFERENCES [TestCases] ([id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TestResults_Env] FOREIGN KEY ([env_id]) REFERENCES [TestEnvironments] ([id])
);
CREATE INDEX [IX_TestResults_behalf_of_id] ON [TestResults] ([behalf_of_id]);
CREATE INDEX [IX_TestResults_by_id] ON [TestResults] ([by_id]);
CREATE INDEX [IX_TestResults_Case_Env] ON [TestResults] ([test_case_id], [env_id], [at] DESC, [id] DESC);
CREATE INDEX [IX_TestResults_env_id] ON [TestResults] ([env_id]);

CREATE TABLE [TestRetestFlags] (
    [test_case_id] bigint NOT NULL,
    [env_id] bigint NOT NULL,
    [reason] nvarchar(20) NOT NULL,
    [bug_issue_id] bigint NULL,
    [build] nvarchar(80) NULL,
    [req_key] nvarchar(120) NULL,
    [at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_TestRetestFlags] PRIMARY KEY ([test_case_id], [env_id]),
    CONSTRAINT [CK_TestRetestFlags_Reason] CHECK ([reason] IN (N'BUG_FIXED', N'REQ_CHANGED', N'REGRESSION', N'MANUAL')),
    CONSTRAINT [FK_TestRetestFlags_Bug] FOREIGN KEY ([bug_issue_id]) REFERENCES [Issues] ([id]),
    CONSTRAINT [FK_TestRetestFlags_Case] FOREIGN KEY ([test_case_id]) REFERENCES [TestCases] ([id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TestRetestFlags_Env] FOREIGN KEY ([env_id]) REFERENCES [TestEnvironments] ([id])
);
CREATE INDEX [IX_TestRetestFlags_bug_issue_id] ON [TestRetestFlags] ([bug_issue_id]);
CREATE INDEX [IX_TestRetestFlags_env_id] ON [TestRetestFlags] ([env_id]);

CREATE TABLE [TestActivities] (
    [id] bigint NOT NULL IDENTITY(1,1),
    [plan_id] bigint NOT NULL,
    [type] nvarchar(20) NOT NULL,
    [at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [actor_id] bigint NULL,
    [env_id] bigint NULL,
    [module_id] bigint NULL,
    [test_case_id] bigint NULL,
    [from_status] nvarchar(20) NULL,
    [to_result] nvarchar(20) NULL,
    [build] nvarchar(80) NULL,
    [behalf_of_id] bigint NULL,
    [text] nvarchar(500) NULL,
    CONSTRAINT [PK_TestActivities] PRIMARY KEY ([id]),
    CONSTRAINT [CK_TestActivities_Type] CHECK ([type] IN (N'RESULT', N'ASSIGN', N'BUG', N'REQ', N'TESTCASE', N'BUILD', N'SETUP', N'HANDOVER', N'DEPLOY', N'CASE')),
    CONSTRAINT [FK_TestActivities_Actor] FOREIGN KEY ([actor_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestActivities_BehalfOf] FOREIGN KEY ([behalf_of_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestActivities_Case] FOREIGN KEY ([test_case_id]) REFERENCES [TestCases] ([id]),
    CONSTRAINT [FK_TestActivities_Env] FOREIGN KEY ([env_id]) REFERENCES [TestEnvironments] ([id]),
    CONSTRAINT [FK_TestActivities_Module] FOREIGN KEY ([module_id]) REFERENCES [TestModules] ([id]),
    CONSTRAINT [FK_TestActivities_Plan] FOREIGN KEY ([plan_id]) REFERENCES [TestPlans] ([id]) ON DELETE CASCADE
);
CREATE INDEX [IX_TestActivities_actor_id] ON [TestActivities] ([actor_id]);
CREATE INDEX [IX_TestActivities_behalf_of_id] ON [TestActivities] ([behalf_of_id]);
CREATE INDEX [IX_TestActivities_env_id] ON [TestActivities] ([env_id]);
CREATE INDEX [IX_TestActivities_module_id] ON [TestActivities] ([module_id]);
CREATE INDEX [IX_TestActivities_Plan_At] ON [TestActivities] ([plan_id], [at] DESC, [id] DESC);
CREATE INDEX [IX_TestActivities_test_case_id] ON [TestActivities] ([test_case_id]);

CREATE TABLE [TestAssignments] (
    [module_id] bigint NOT NULL,
    [env_id] bigint NOT NULL,
    [assignee_id] bigint NOT NULL,
    CONSTRAINT [PK_TestAssignments] PRIMARY KEY ([module_id], [env_id]),
    CONSTRAINT [FK_TestAssignments_Assignee] FOREIGN KEY ([assignee_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_TestAssignments_Env] FOREIGN KEY ([env_id]) REFERENCES [TestEnvironments] ([id]),
    CONSTRAINT [FK_TestAssignments_Module] FOREIGN KEY ([module_id]) REFERENCES [TestModules] ([id]) ON DELETE CASCADE
);
CREATE INDEX [IX_TestAssignments_Assignee] ON [TestAssignments] ([assignee_id]);
CREATE INDEX [IX_TestAssignments_env_id] ON [TestAssignments] ([env_id]);
");

            // Batch 5: Seed Permission 51 & data migrations S-W1 and S-R1
            migrationBuilder.Sql(@"
UPDATE [Permissions] SET [sort_order] = 47 WHERE [id] = 45;
UPDATE [Permissions] SET [sort_order] = 48 WHERE [id] = 46;
UPDATE [Permissions] SET [sort_order] = 49 WHERE [id] = 47;
UPDATE [Permissions] SET [sort_order] = 50 WHERE [id] = 48;
UPDATE [Permissions] SET [name] = N'Chạy Test Case, ghi kết quả (Pass / Fail / N/A) & gắn bug có sẵn' WHERE [id] = 50;

IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [id] = 51)
BEGIN
    SET IDENTITY_INSERT [Permissions] ON;
    INSERT INTO [Permissions] ([id], [code], [description], [group_name], [is_active], [name], [scope], [sort_order])
    VALUES (51, 'testcase.plan', NULL, N'6. Kiểm thử & Truy xuất', 1, N'Thiết lập đợt test (môi trường, module), phân công, regression & bàn giao', 'PROJECT', 46);
    SET IDENTITY_INSERT [Permissions] OFF;
END;

EXEC(N'
;WITH firstActive AS (
  SELECT id, ROW_NUMBER() OVER (PARTITION BY project_id ORDER BY position, id) AS rn
  FROM ProjectPhases WHERE is_active = 1
)
UPDATE p SET lifecycle_status = CASE WHEN f.rn = 1 THEN N''ACTIVE'' ELSE N''UPCOMING'' END
FROM ProjectPhases p JOIN firstActive f ON f.id = p.id;
');

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
            migrationBuilder.Sql(@"
DELETE FROM RolePermissions WHERE permission_id = 51;
DELETE FROM Permissions WHERE id = 51;
UPDATE [Permissions] SET [sort_order] = 46 WHERE [id] = 45;
UPDATE [Permissions] SET [sort_order] = 47 WHERE [id] = 46;
UPDATE [Permissions] SET [sort_order] = 48 WHERE [id] = 47;
UPDATE [Permissions] SET [sort_order] = 49 WHERE [id] = 48;
DROP TABLE IF EXISTS [ProjectPhaseLogs];
DROP TABLE IF EXISTS [TestActivities];
DROP TABLE IF EXISTS [TestAssignments];
DROP TABLE IF EXISTS [TestBuilds];
DROP TABLE IF EXISTS [TestResults];
DROP TABLE IF EXISTS [TestRetestFlags];
DROP TABLE IF EXISTS [TestCaseRequirementLinks];
DROP TABLE IF EXISTS [TestCases];
DROP TABLE IF EXISTS [TestSections];
DROP TABLE IF EXISTS [TestEnvironments];
DROP TABLE IF EXISTS [TestModules];
DROP TABLE IF EXISTS [TestPlans];
");
        }
    }
}
