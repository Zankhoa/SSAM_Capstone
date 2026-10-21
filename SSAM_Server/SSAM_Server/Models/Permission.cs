using SSAM_Server.Security;

namespace SSAM_Server.Models;

public class Permission
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = RoleScopes.Tenant;
    public string GroupName { get; set; } = string.Empty;
    public bool IsSensitive { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
