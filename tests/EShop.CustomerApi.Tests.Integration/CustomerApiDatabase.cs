using EShop.Data.Entities;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;

[assembly: AssemblyFixture(typeof(EShop.CustomerApi.Tests.Integration.CustomerApiDatabase))]
// All tests of this project share one database and empty it before each test, so they must not run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.CustomerApi.Tests.Integration;

public sealed class CustomerApiDatabase : TestDatabase
{
    public CustomerApiDatabase()
        : base("customerapi") { }

    public static CustomerProfile NewProfile(string userId) =>
        new()
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    public static CustomerAddress NewAddress(string userId, bool isDefault = false) =>
        new()
        {
            UserId = userId,
            RecipientName = "Jane Doe",
            Address = "1 Main St",
            City = "Berlin",
            PostalCode = "10115",
            Country = "Germany",
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow,
        };

    // Seeds the address and, on the user's first address, the profile its foreign key needs.
    public async Task<CustomerAddress> SeedAddressAsync(
        CustomerAddress address,
        CancellationToken cancellationToken
    )
    {
        await using var db = CreateContext();
        if (!await db.CustomerProfiles.AnyAsync(p => p.UserId == address.UserId, cancellationToken))
        {
            db.CustomerProfiles.Add(NewProfile(address.UserId));
        }
        db.CustomerAddresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        return address;
    }

    public async Task<CustomerProfile?> LoadProfileAsync(
        string userId,
        CancellationToken cancellationToken
    )
    {
        await using var db = CreateContext();
        return await db
            .CustomerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<List<CustomerAddress>> LoadAddressesAsync(
        string userId,
        CancellationToken cancellationToken
    )
    {
        await using var db = CreateContext();
        return await db
            .CustomerAddresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.AddressId)
            .ToListAsync(cancellationToken);
    }
}
