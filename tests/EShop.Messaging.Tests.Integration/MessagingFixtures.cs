using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;

[assembly: AssemblyFixture(typeof(EShop.Messaging.Tests.Integration.MessagingDatabase))]
[assembly: AssemblyFixture(typeof(RabbitMqFixture))]
// One database and one broker for all tests of this project, emptied between tests: no parallel runs.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.Messaging.Tests.Integration;

public sealed class MessagingDatabase : TestDatabase<MessagingTestDbContext>
{
    public MessagingDatabase()
        : base("messaging") { }

    protected override MessagingTestDbContext NewContext(
        DbContextOptions<MessagingTestDbContext> options
    ) => new(options);

    // No migrations for a test model: created from it. Deleted first, because EnsureCreated does nothing in a database
    // that already has tables (a reused server, ConnectionStrings__EShopTests).
    protected override async Task CreateSchemaAsync(
        MessagingTestDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
