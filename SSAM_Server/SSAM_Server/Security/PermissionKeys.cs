namespace SSAM_Server.Security;

public static class RoleScopes
{
    public const string Tenant = "TENANT";
    public const string Workspace = "WORKSPACE";
    public const string Project = "PROJECT";

    public static bool IsValid(string? scope) =>
        scope is Tenant or Workspace or Project;
}

public static class RoleKinds
{
    public const string Ba = "BA";
    public const string Designer = "DESIGNER";
    public const string Qa = "QA";
    public const string Dev = "DEV";
    public const string Pm = "PM";
    public const string Other = "OTHER";

    public static bool IsValid(string? kind) =>
        kind is Ba or Designer or Qa or Dev or Pm or Other;
}

public static class PermissionKeys
{
    // TENANT scope - Phạm vi Quản trị Tổ chức (12)
    public const string CompanyView = "company.view";
    public const string CompanyUpdate = "company.update";
    public const string UserView = "user.view";
    public const string UserInvite = "user.invite";
    public const string UserUpdate = "user.update";
    public const string UserDeactivate = "user.deactivate";
    public const string RoleView = "role.view";
    public const string RoleManage = "role.manage";
    public const string UserAssignRole = "user.assign_role";
    public const string WorkspaceCreate = "workspace.create";
    public const string WorkspaceViewAll = "workspace.view_all";
    public const string AuditView = "audit.view";

    // WORKSPACE scope - Phạm vi Không gian làm việc (8)
    public const string WorkspaceView = "workspace.view";
    public const string WorkspaceUpdate = "workspace.update";
    public const string WorkspaceArchive = "workspace.archive";
    public const string WorkspaceMemberView = "workspace.member.view";
    public const string WorkspaceMemberManage = "workspace.member.manage";
    public const string WorkspaceMemberAssignRole = "workspace.member.assign_role";
    public const string ProjectCreate = "project.create";
    public const string ProjectInheritAccess = "project.inherit_access";

    // PROJECT scope - Phạm vi Thực thi dự án (29)
    // 1. Quản lý Project
    public const string ProjectView = "project.view";
    public const string ProjectUpdate = "project.update";
    public const string ProjectArchive = "project.archive";
    public const string ProjectMemberManage = "project.member.manage";
    // 2. Quản lý Phase & Quy trình
    public const string ProjectWorkflowView = "project.workflow.view";
    public const string ProjectWorkflowManage = "project.workflow.manage";
    public const string ProjectWorkflowTransitionManage = "project.workflow.transition.manage";
    // 3. Quản lý Issue & Công việc
    public const string IssueView = "issue.view";
    public const string IssueCreate = "issue.create";
    public const string IssueUpdate = "issue.update";
    public const string IssueAssign = "issue.assign";
    public const string IssueChangeStatus = "issue.change_status";
    public const string IssueDelete = "issue.delete";
    public const string SprintManage = "sprint.manage";
    // 4. Tài liệu Đặc tả
    public const string DocumentView = "document.view";
    public const string DocumentEdit = "document.edit";
    public const string DocumentApprove = "document.approve";
    public const string DocumentRevise = "document.revise";
    public const string DocumentDelete = "document.delete";
    // 5. UX/UI
    public const string FigmaCreate = "figma.create";
    public const string FigmaUpdate = "figma.update";
    public const string FigmaArchive = "figma.archive";
    // 6. Kiểm thử & Truy xuất
    public const string TestCaseView = "testcase.view";
    public const string TestCaseManage = "testcase.manage";
    public const string TestCaseExecute = "testcase.execute";
    // 7. Bình luận & Cộng tác
    public const string CommentView = "comment.view";
    public const string CommentWrite = "comment.write";
    // 8. Lịch sử & Tiến độ
    public const string ActivityView = "activity.view";
    // 9. Hỗ trợ AI
    public const string AiUse = "ai.use";

    // Aliases for Module A/C/D service checks mapped to the 49-permission catalog
    public const string DocumentCreate = DocumentEdit;
    public const string DocumentUpdate = DocumentEdit;
    public const string DocumentComment = CommentWrite;
    public const string IssueTransition = IssueChangeStatus;
    public const string IssueComment = CommentWrite;
    public const string TestcaseView = TestCaseView;
    public const string TestcaseCreate = TestCaseManage;
    public const string TestcaseUpdate = TestCaseManage;
    public const string TestcaseDelete = TestCaseManage;
    public const string TestcaseExecute = TestCaseExecute;
    public const string WorkspaceSettingsManage = WorkspaceUpdate;
    public const string CompanyRolesManage = RoleManage;

    public static readonly string[] AllTenantKeys =
    [
        CompanyView,
        CompanyUpdate,
        UserView,
        UserInvite,
        UserUpdate,
        UserDeactivate,
        RoleView,
        RoleManage,
        UserAssignRole,
        WorkspaceCreate,
        WorkspaceViewAll,
        AuditView
    ];

    public static readonly string[] AllWorkspaceKeys =
    [
        WorkspaceView,
        WorkspaceUpdate,
        WorkspaceArchive,
        WorkspaceMemberView,
        WorkspaceMemberManage,
        WorkspaceMemberAssignRole,
        ProjectCreate,
        ProjectInheritAccess
    ];

    public static readonly string[] AllProjectKeys =
    [
        ProjectView,
        ProjectUpdate,
        ProjectArchive,
        ProjectMemberManage,
        ProjectWorkflowView,
        ProjectWorkflowManage,
        ProjectWorkflowTransitionManage,
        IssueView,
        IssueCreate,
        IssueUpdate,
        IssueAssign,
        IssueChangeStatus,
        IssueDelete,
        SprintManage,
        DocumentView,
        DocumentEdit,
        DocumentApprove,
        DocumentRevise,
        DocumentDelete,
        FigmaCreate,
        FigmaUpdate,
        FigmaArchive,
        TestCaseView,
        TestCaseManage,
        TestCaseExecute,
        CommentView,
        CommentWrite,
        ActivityView,
        AiUse
    ];

    public static IReadOnlyList<string> KeysOfScope(string scope) => scope switch
    {
        RoleScopes.Tenant => AllTenantKeys,
        RoleScopes.Workspace => AllWorkspaceKeys,
        RoleScopes.Project => AllProjectKeys,
        _ => Array.Empty<string>()
    };
}
