using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Common.Options;
using SSAM_Server.Data;
using SSAM_Server.UnitTests.Helpers;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.UnitTests.Services.InvitationServiceTests;

public abstract class InvitationServiceTestBase
{
    protected readonly Mock<IInvitationRepository> InvitationRepositoryMock;
    protected readonly Mock<ICompanyRepository> CompanyRepositoryMock;
    protected readonly Mock<IAuthRepository> AuthRepositoryMock;
    protected readonly Mock<IWorkspaceRepository> WorkspaceRepositoryMock;
    protected readonly Mock<IPermissionService> PermissionServiceMock;
    protected readonly Mock<IPasswordHasher> PasswordHasherMock;
    protected readonly Mock<IJwtTokenGenerator> JwtTokenGeneratorMock;
    protected readonly Mock<IEmailService> EmailServiceMock;
    protected readonly Mock<ICurrentUser> CurrentUserMock;
    protected readonly ClientAppOptions ClientAppOptions;
    protected readonly FakeTimeProvider Clock;
    protected readonly AppDbContext Db;
    protected readonly SSAM_Server.Services.InvitationService InvitationService;

    protected InvitationServiceTestBase()
    {
        InvitationRepositoryMock = new Mock<IInvitationRepository>();
        CompanyRepositoryMock = new Mock<ICompanyRepository>();
        AuthRepositoryMock = new Mock<IAuthRepository>();
        WorkspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        PermissionServiceMock = new Mock<IPermissionService>();
        PasswordHasherMock = new Mock<IPasswordHasher>();
        JwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        EmailServiceMock = new Mock<IEmailService>();
        CurrentUserMock = new Mock<ICurrentUser>();
        ClientAppOptions = new ClientAppOptions
        {
            FrontendBaseUrl = "http://localhost:5174"
        };
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));
        Db = TestDbContextFactory.Create(Clock);

        InvitationService = new SSAM_Server.Services.InvitationService(
            InvitationRepositoryMock.Object,
            CompanyRepositoryMock.Object,
            AuthRepositoryMock.Object,
            WorkspaceRepositoryMock.Object,
            Db,
            PermissionServiceMock.Object,
            PasswordHasherMock.Object,
            JwtTokenGeneratorMock.Object,
            EmailServiceMock.Object,
            CurrentUserMock.Object,
            Options.Create(ClientAppOptions),
            Clock);
    }
}