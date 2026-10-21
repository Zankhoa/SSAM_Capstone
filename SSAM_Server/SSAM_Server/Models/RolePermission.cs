namespace SSAM_Server.Models;

public class RolePermission
{
    public long RoleId { get; set; }
    public CompanyRole Role { get; set; } = null!;

    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
