using EShop.Tests.Common;

[assembly: AssemblyFixture(typeof(EShop.ProductApi.Tests.Integration.ProductApiDatabase))]
// All tests of this project share one database and empty it before each test, so they must not run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.ProductApi.Tests.Integration;

public sealed class ProductApiDatabase : TestDatabase
{
    public ProductApiDatabase()
        : base("productapi") { }
}
