using SSAM_Server.DTOs.Common;
using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Security;

public record ProjectAccessContext(
    Project Project,
    Workspace Workspace,
    HashSet<string> Permissions,
    RoleRefDto? MyRole);
