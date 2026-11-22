using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Data;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.UnitTests.Helpers;

namespace SSAM_Server.UnitTests.Services.TestingServiceTests;

public abstract class TestingServiceTestBase : IDisposable
{
    protected readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero));
    protected readonly AppDbContext Db;
    protected readonly Mock<ICurrentUser> CurrentUserMock = new();
    protected readonly Mock<IProjectAccessGuard> AccessGuardMock = new();
    protected readonly Mock<IKanbanService> KanbanServiceMock = new();
    protected readonly Mock<IAiService> AiServiceMock = new();
    protected readonly Mock<IFigmaService> FigmaServiceMock = new();
    protected readonly SSAM_Server.Services.TestingService TestingService;

    protected TestingServiceTestBase()
    {
        Db = TestDbContextFactory.Create(Clock);
        TestingService = new SSAM_Server.Services.TestingService(
            Db,
            CurrentUserMock.Object,
            AccessGuardMock.Object,
            KanbanServiceMock.Object,
            Clock,
            AiServiceMock.Object,
            FigmaServiceMock.Object);
    }

    public void Dispose() => Db.Dispose();
}
