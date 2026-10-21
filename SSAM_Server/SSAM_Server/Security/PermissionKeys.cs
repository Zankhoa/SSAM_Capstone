namespace SSAM_Server.Security;

public static class RoleScopes
{
    public const string Tenant = "TENANT";
    public const string Workspace = "WORKSPACE";
    public const string Project = "PROJECT";

    public static bool IsValid(string? scope) =>
        scope is Tenant or Workspace or Project;
}

public static class PermissionKeys
{
    // TENANT scope (1..9)
    public const string CompanyUpdate = "company.update";
    public const string UserView = "user.view";
    public const string UserInvite = "user.invite";
    public const string UserAssignRole = "user.assign_role";
    public const string UserDeactivate = "user.deactivate";
    public const string RoleView = "role.view";
    public const string RoleManage = "role.manage";
    public const string WorkspaceCreate = "workspace.create";
    public const string WorkspaceViewAll = "workspace.view_all";

    // WORKSPACE scope (10..15)
    public const string WorkspaceUpdate = "workspace.update";
    public const string WorkspaceArchive = "workspace.archive";
    public const string WorkspaceMemberView = "workspace.member.view";
    public const string WorkspaceMemberManage = "workspace.member.manage";
    public const string ProjectCreate = "project.create";
    public const string ProjectViewAll = "project.view_all";

    // PROJECT scope (16..21)
    public const string ProjectUpdate = "project.update";
    public const string ProjectArchive = "project.archive";
    public const string ProjectAiToggle = "project.ai.toggle";
    public const string ProjectMemberView = "project.member.view";
    public const string ProjectMemberManage = "project.member.manage";
    public const string ProjectWorkflowManage = "project.workflow.manage";

    public static readonly string[] AllTenantKeys =
    [
        CompanyUpdate,
        UserView,
        UserInvite,
        UserAssignRole,
        UserDeactivate,
        RoleView,
        RoleManage,
        WorkspaceCreate,
        WorkspaceViewAll
    ];

    public static readonly string[] AllWorkspaceKeys =
    [
        WorkspaceUpdate,
        WorkspaceArchive,
        WorkspaceMemberView,
        WorkspaceMemberManage,
        ProjectCreate,
        ProjectViewAll
    ];

    public static readonly string[] AllProjectKeys =
    [
        ProjectUpdate,
        ProjectArchive,
        ProjectAiToggle,
        ProjectMemberView,
        ProjectMemberManage,
        ProjectWorkflowManage
    ];

    public static IReadOnlyList<string> KeysOfScope(string scope) => scope switch
    {
        RoleScopes.Tenant => AllTenantKeys,
        RoleScopes.Workspace => AllWorkspaceKeys,
        RoleScopes.Project => AllProjectKeys,
        _ => Array.Empty<string>()
    };
}
