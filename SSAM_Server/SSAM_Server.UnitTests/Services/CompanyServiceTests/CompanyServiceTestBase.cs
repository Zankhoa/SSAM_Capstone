using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Interfaces.Security;

namespace SSAM_Server.UnitTests.Services.CompanyServiceTests;

public abstract class CompanyServiceTestBase
{
    protected readonly Mock<ICompanyRepository> CompanyRepositoryMock = new();
    protected readonly Mock<IAuthRepository> AuthRepositoryMock = new();
    protected readonly Mock<IPermissionService> PermissionServiceMock = new();
    protected readonly Mock<ICurrentUser> CurrentUserMock = new();
    protected readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));
    protected readonly SSAM_Server.Services.CompanyService CompanyService;

    protected CompanyServiceTestBase()
    {
        CompanyService = new SSAM_Server.Services.CompanyService(
            CompanyRepositoryMock.Object,
            AuthRepositoryMock.Object,
            PermissionServiceMock.Object,
            CurrentUserMock.Object,
            Clock
        );
    }
}