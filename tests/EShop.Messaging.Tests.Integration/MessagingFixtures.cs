using EShop.Tests.Common;

[assembly: AssemblyFixture(typeof(EShop.Messaging.Tests.Integration.MessagingDatabase))]
[assembly: AssemblyFixture(typeof(RabbitMqFixture))]
// One database and one broker for all tests of this project, emptied between tests: no parallel runs.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.Messaging.Tests.Integration;

public sealed class MessagingDatabase : TestDatabase
{
    public MessagingDatabase()
        : base("messaging") { }
}
