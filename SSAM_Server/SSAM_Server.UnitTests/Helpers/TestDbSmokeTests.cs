using SSAM_Server.Models;

namespace SSAM_Server.UnitTests.Helpers;

public class TestDbSmokeTests
{
    [Fact]
    public async Task InMemoryDb_CanAddAndQueryEntities()
    {
        await using var db = TestDbContextFactory.Create();
        db.DocumentNodes.Add(new DocumentNode { ProjectId = 1, Title = "x", DocType = DocumentTypes.Srs });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, db.DocumentNodes.Count());
    }
}
