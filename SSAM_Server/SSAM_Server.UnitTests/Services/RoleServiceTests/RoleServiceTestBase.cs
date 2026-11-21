using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Interfaces.Security;

namespace SSAM_Server.UnitTests.Services.RoleServiceTests;

public abstract class RoleServiceTestBase
{
    protected readonly Mock<IRoleRepository> RoleRepositoryMock = new();
    protected readonly Mock<IPermissionService> PermissionServiceMock = new();
    protected readonly Mock<ICurrentUser> CurrentUserMock = new();
    protected readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero));
    protected readonly SSAM_Server.Services.RoleService RoleService;

    protected RoleServiceTestBase()
    {
        RoleService = new SSAM_Server.Services.RoleService(
            RoleRepositoryMock.Object,
            PermissionServiceMock.Object,
            CurrentUserMock.Object,
            Clock
        );
    }
}