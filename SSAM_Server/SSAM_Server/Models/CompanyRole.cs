using SSAM_Server.Security;

namespace SSAM_Server.Models;

public static class CompanyRoleCodes
{
    // TENANT scope
    public const string TenantAdmin = "TENANT_ADMIN";
    public const string Member = "MEMBER";

    // WORKSPACE scope
    public const string WorkspaceAdmin = "WORKSPACE_ADMIN";
    public const string ProjectInitiator = "PROJECT_INITIATOR";
    public const string WorkspaceMember = "WORKSPACE_MEMBER";

    // PROJECT scope
    public const string ProjectManager = "PROJECT_MANAGER";
    public const string TechLead = "TECH_LEAD";
    public const string Ba = "BA";
    public const string Designer = "DESIGNER";
    public const string Developer = "DEVELOPER";
    public const string Qa = "QA";

    public static bool IsLocked(string? code) =>
        code is TenantAdmin or WorkspaceAdmin or ProjectManager;
}

public class CompanyRole : IHasUpdatedAt
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Scope { get; set; } = RoleScopes.Tenant;
    public string Kind { get; set; } = RoleKinds.Other;
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

