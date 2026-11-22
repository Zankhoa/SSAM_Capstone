using Microsoft.Extensions.Time.Testing;
using Moq;
using SSAM_Server.Data;
using SSAM_Server.Interfaces.Security;
using SSAM_Server.Interfaces.Services;
using SSAM_Server.UnitTests.Helpers;

namespace SSAM_Server.UnitTests.Services.DocumentServiceTests;

public abstract class DocumentServiceTestBase : IDisposable
{
    protected readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero));
    protected readonly AppDbContext Db;
    protected readonly Mock<ICurrentUser> CurrentUserMock = new();
    protected readonly Mock<IPermissionService> PermissionServiceMock = new();
    protected readonly Mock<IAiService> AiServiceMock = new();
    protected readonly Mock<IProjectAccessGuard> AccessGuardMock = new();
    protected readonly Mock<IRequirementSyncService> RequirementSyncMock = new();
    protected readonly SSAM_Server.Services.DocumentService DocumentService;

    protected DocumentServiceTestBase()
    {
        Db = TestDbContextFactory.Create(Clock);
        DocumentService = new SSAM_Server.Services.DocumentService(
            Db,
            CurrentUserMock.Object,
            PermissionServiceMock.Object,
            Clock,
            AiServiceMock.Object,
            AccessGuardMock.Object,
            RequirementSyncMock.Object);
    }

    public void Dispose() => Db.Dispose();
}
