using EShop.Tests.Common;

[assembly: AssemblyFixture(typeof(EShop.CategoryApi.Tests.Integration.CategoryApiDatabase))]
// All tests of this project share one database and empty it before each test, so they must not run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.CategoryApi.Tests.Integration;

public sealed class CategoryApiDatabase : TestDatabase
{
    public CategoryApiDatabase()
        : base("categoryapi") { }
}
