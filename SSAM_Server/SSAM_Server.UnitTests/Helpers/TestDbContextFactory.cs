using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SSAM_Server.Data;

namespace SSAM_Server.UnitTests.Helpers;

/// <summary>Creates an isolated in-memory AppDbContext per call (unique database name).</summary>
public static class TestDbContextFactory
{
    public static AppDbContext Create(TimeProvider? clock = null, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options, clock);
    }
}
