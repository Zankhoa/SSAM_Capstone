using SSAM_Server.DTOs.Common;
using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Security;

public record WorkspaceAccessContext(
    Workspace Workspace,
    HashSet<string> Permissions,
    RoleRefDto? MyRole);
