using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.Interfaces.Security;

namespace SSAM_Server.UnitTests.Services.AuthService;

public abstract class AuthServiceTestBase
{
    protected readonly Mock<IAuthRepository> AuthRepositoryMock = new();
    protected readonly Mock<IPasswordHasher> PasswordHasherMock = new();
    protected readonly Mock<IJwtTokenGenerator> JwtTokenGeneratorMock = new();
    protected readonly Mock<IEmailService> EmailServiceMock = new();
    protected readonly Mock<IGoogleTokenValidator> GoogleTokenValidatorMock = new();
    protected readonly Mock<ICurrentUser> CurrentUserMock = new();
    protected readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
    protected readonly SSAM_Server.Services.AuthService AuthService;

    protected AuthServiceTestBase()
    {
        AuthService = new SSAM_Server.Services.AuthService(
            AuthRepositoryMock.Object,
            PasswordHasherMock.Object,
            JwtTokenGeneratorMock.Object,
            EmailServiceMock.Object,
            GoogleTokenValidatorMock.Object,
            CurrentUserMock.Object,
            Clock
        );
    }
}