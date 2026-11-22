using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Interfaces.Security;

namespace SSAM_Server.UnitTests.Services.WorkspaceServiceTests;

public abstract class WorkspaceServiceTestBase
{
    protected readonly Mock<IWorkspaceRepository> WorkspaceRepositoryMock;
    protected readonly Mock<ICompanyRepository> CompanyRepositoryMock;
    protected readonly Mock<IPermissionService> PermissionServiceMock;
    protected readonly Mock<ICurrentUser> CurrentUserMock;
    protected readonly FakeTimeProvider Clock;
    protected readonly SSAM_Server.Data.AppDbContext Db;
    protected readonly SSAM_Server.Services.WorkspaceService WorkspaceService;

    protected WorkspaceServiceTestBase()
    {
        WorkspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        CompanyRepositoryMock = new Mock<ICompanyRepository>();
        PermissionServiceMock = new Mock<IPermissionService>();
        CurrentUserMock = new Mock<ICurrentUser>();
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero));
        Db = SSAM_Server.UnitTests.Helpers.TestDbContextFactory.Create(Clock);

        WorkspaceService = new SSAM_Server.Services.WorkspaceService(
            WorkspaceRepositoryMock.Object,
            CompanyRepositoryMock.Object,
            Db,
            PermissionServiceMock.Object,
            CurrentUserMock.Object,
            Clock);
    }
}
