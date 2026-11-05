using EShop.CustomerApi.Dtos;
using EShop.CustomerApi.Repositories;
using EShop.Data.Entities;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EShop.CustomerApi.Tests.Integration.Repositories;

// Arrange, act and assert each use their own DbContext, so assertions read what is stored in PostgreSQL.
public class CustomerRepositoryTests : IAsyncLifetime
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";
    private const string Phone = "+49 30 1234567";

    private static readonly AddressRequest Request = new()
    {
        RecipientName = "Jane Doe",
        Address = "1 Main St",
        City = "Berlin",
        PostalCode = "10115",
        Country = "Germany",
    };

    private readonly CustomerApiDatabase _database;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public CustomerRepositoryTests(CustomerApiDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RetrievePhone_NoProfile_ReturnsNullAndCreatesNoProfile()
    {
        var phone = await ActAsync(r => r.RetrievePhoneAsync(UserId, _cancellationToken));

        phone.ShouldBeNull();
        (await _database.LoadProfileAsync(UserId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePhone_NoProfile_CreatesProfileWithTrimmedPhone()
    {
        await ActAsync(r => r.UpdatePhoneAsync(UserId, $" {Phone} ", _cancellationToken));

        (await _database.LoadProfileAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .Phone.ShouldBe(Phone);
    }

    [Fact]
    public async Task UpdatePhone_Empty_RemovesPhone()
    {
        var profile = CustomerApiDatabase.NewProfile(UserId);
        profile.Phone = Phone;
        await _database.SeedAsync(profile, _cancellationToken);

        await ActAsync(r => r.UpdatePhoneAsync(UserId, "", _cancellationToken));

        (await _database.LoadProfileAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .Phone.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePhone_ProfileCreatedConcurrently_UpdatesThatProfile()
    {
        // Another first request of the same user inserts the profile between this one's read and its insert.
        await ActAsync(
            r => r.UpdatePhoneAsync(UserId, Phone, _cancellationToken),
            new BeforeSaveChangesInterceptor(() =>
                _database.SeedAsync(CustomerApiDatabase.NewProfile(UserId), _cancellationToken)
            )
        );

        (await _database.LoadProfileAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .Phone.ShouldBe(Phone);
    }

    [Fact]
    public async Task AddAddress_FirstAddress_CreatesProfileAndMakesAddressDefault()
    {
        var result = await ActAsync(r => r.AddAddressAsync(UserId, Request, _cancellationToken));

        result.Status.ShouldBe(SaveAddressStatus.Saved);
        result.Address.ShouldNotBeNull().IsDefault.ShouldBeTrue();
        (await _database.LoadProfileAsync(UserId, _cancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task AddAddress_DefaultRequested_ClearsPreviousDefault()
    {
        var previous = await SeedAddressAsync(UserId, isDefault: true);

        var result = await ActAsync(r =>
            r.AddAddressAsync(UserId, Request with { IsDefault = true }, _cancellationToken)
        );

        var addresses = await _database.LoadAddressesAsync(UserId, _cancellationToken);
        addresses
            .Single(a => a.IsDefault)
            .AddressId.ShouldBe(result.Address.ShouldNotBeNull().AddressId);
        addresses.Single(a => a.AddressId == previous.AddressId).IsDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAddress_NotDefault_KeepsPreviousDefault()
    {
        var previous = await SeedAddressAsync(UserId, isDefault: true);

        await ActAsync(r => r.AddAddressAsync(UserId, Request, _cancellationToken));

        (await _database.LoadAddressesAsync(UserId, _cancellationToken))
            .Single(a => a.IsDefault)
            .AddressId.ShouldBe(previous.AddressId);
    }

    [Fact]
    public async Task AddAddress_DefaultRequested_KeepsOtherUsersDefault()
    {
        await SeedAddressAsync(OtherUserId, isDefault: true);

        await ActAsync(r =>
            r.AddAddressAsync(UserId, Request with { IsDefault = true }, _cancellationToken)
        );

        (await _database.LoadAddressesAsync(OtherUserId, _cancellationToken))
            .ShouldHaveSingleItem()
            .IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task AddAddress_LimitReached_ReturnsLimitReachedAndStoresNothing()
    {
        for (var i = 0; i < CustomerRepository.MaxAddresses; i++)
        {
            await SeedAddressAsync(UserId);
        }

        var result = await ActAsync(r => r.AddAddressAsync(UserId, Request, _cancellationToken));

        result.Status.ShouldBe(SaveAddressStatus.LimitReached);
        (await _database.LoadAddressesAsync(UserId, _cancellationToken)).Count.ShouldBe(
            CustomerRepository.MaxAddresses
        );
    }

    [Fact]
    public async Task UpdateAddress_OtherUsersAddress_ReturnsNotFoundAndChangesNothing()
    {
        var others = await SeedAddressAsync(OtherUserId);

        var result = await ActAsync(r =>
            r.UpdateAddressAsync(
                UserId,
                others.AddressId,
                Request with
                {
                    City = "Paris",
                },
                _cancellationToken
            )
        );

        result.Status.ShouldBe(SaveAddressStatus.NotFound);
        (await _database.LoadAddressesAsync(OtherUserId, _cancellationToken))
            .ShouldHaveSingleItem()
            .City.ShouldBe("Berlin");
    }

    [Fact]
    public async Task UpdateAddress_DefaultSetConcurrently_ReturnsDefaultConflictAndChangesNothing()
    {
        var address = await SeedAddressAsync(UserId);

        // Another request makes a new address the default between this one's UPDATE and its save.
        var result = await ActAsync(
            r =>
                r.UpdateAddressAsync(
                    UserId,
                    address.AddressId,
                    Request with
                    {
                        City = "Paris",
                        IsDefault = true,
                    },
                    _cancellationToken
                ),
            new BeforeSaveChangesInterceptor(() =>
                _database.SeedAddressAsync(
                    CustomerApiDatabase.NewAddress(UserId, isDefault: true),
                    _cancellationToken
                )
            )
        );

        result.Status.ShouldBe(SaveAddressStatus.DefaultConflict);
        var stored = (await _database.LoadAddressesAsync(UserId, _cancellationToken)).Single(a =>
            a.AddressId == address.AddressId
        );
        stored.City.ShouldBe("Berlin");
        stored.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAddress_OwnAddress_DeletesIt()
    {
        var address = await SeedAddressAsync(UserId);

        var deleted = await ActAsync(r =>
            r.DeleteAddressAsync(UserId, address.AddressId, _cancellationToken)
        );

        deleted.ShouldBeTrue();
        (await _database.LoadAddressesAsync(UserId, _cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAddress_OtherUsersAddress_ReturnsFalseAndKeepsIt()
    {
        var others = await SeedAddressAsync(OtherUserId);

        var deleted = await ActAsync(r =>
            r.DeleteAddressAsync(UserId, others.AddressId, _cancellationToken)
        );

        deleted.ShouldBeFalse();
        (
            await _database.LoadAddressesAsync(OtherUserId, _cancellationToken)
        ).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RetrieveAddresses_ReturnsOwnAddressesDefaultFirstThenNewest()
    {
        var older = await SeedAddressAsync(UserId);
        var defaultAddress = await SeedAddressAsync(UserId, isDefault: true);
        var newer = await SeedAddressAsync(UserId);
        await SeedAddressAsync(OtherUserId);

        var addresses = await ActAsync(r => r.RetrieveAddressesAsync(UserId, _cancellationToken));

        addresses
            .Select(a => a.AddressId)
            .ShouldBe(new[] { defaultAddress.AddressId, newer.AddressId, older.AddressId });
    }

    private Task<CustomerAddress> SeedAddressAsync(string userId, bool isDefault = false) =>
        _database.SeedAddressAsync(
            CustomerApiDatabase.NewAddress(userId, isDefault),
            _cancellationToken
        );

    private async Task<T> ActAsync<T>(
        Func<CustomerRepository, Task<T>> act,
        params IInterceptor[] interceptors
    )
    {
        await using var db = _database.CreateContext(interceptors);
        return await act(new CustomerRepository(db));
    }

    private async Task ActAsync(
        Func<CustomerRepository, Task> act,
        params IInterceptor[] interceptors
    )
    {
        await using var db = _database.CreateContext(interceptors);
        await act(new CustomerRepository(db));
    }
}
