using EShop.NotificationService.Data;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;

[assembly: AssemblyFixture(
    typeof(EShop.NotificationService.Tests.Integration.NotificationServiceDatabase)
)]
[assembly: AssemblyFixture(typeof(RabbitMqFixture))]
// One database and one broker for all tests of this project, emptied between tests: no parallel runs.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.NotificationService.Tests.Integration;

public sealed class NotificationServiceDatabase : TestDatabase<NotificationDbContext>
{
    public NotificationServiceDatabase()
        : base("notificationservice") { }

    protected override NotificationDbContext NewContext(
        DbContextOptions<NotificationDbContext> options
    ) => new(options);
}
