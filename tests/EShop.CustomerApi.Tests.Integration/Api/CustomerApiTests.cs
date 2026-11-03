using System.Net;
using System.Net.Http.Json;
using EShop.CustomerApi.Dtos;
using EShop.CustomerApi.Repositories;
using EShop.Tests.Common;

namespace EShop.CustomerApi.Tests.Integration.Api;

// Real HTTP requests through routing, model binding, [ApiController], JSON and PostgreSQL.
public class CustomerApiTests : IClassFixture<CustomerApiFactory>, IAsyncLifetime
{
    private const string UserId = "user-1";
    private const string ProfileUrl = "api/customers/me";
    private const string AddressesUrl = "api/customers/me/addresses";
    private const string ProblemJson = "application/problem+json";

    private static readonly AddressRequest Request = new()
    {
        RecipientName = "Jane Doe",
        Address = "1 Main St",
        City = "Berlin",
        PostalCode = "10115",
        Country = "Germany",
    };

    private readonly CustomerApiDatabase _database;
    private readonly CustomerApiFactory _factory;
    private readonly HttpClient _client;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public CustomerApiTests(CustomerApiDatabase database, CustomerApiFactory factory)
    {
        _database = database;
        _factory = factory;
        _client = factory.CreateClient().AsUser(UserId);
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetHealth_DatabaseAvailable_ReturnsOk()
    {
        var response = await _client.GetAsync("health", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProfile_Anonymous_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync(ProfileUrl, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_NoProfile_ReturnsEmailFromTokenWithoutPhone()
    {
        var profile = await _client.GetFromJsonAsync<CustomerProfileResponse>(
            ProfileUrl,
            _cancellationToken
        );

        profile.ShouldNotBeNull().Email.ShouldBe($"{UserId}@example.com");
        profile.Phone.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateProfile_ValidPhone_Returns200AndStoresPhone()
    {
        var response = await _client.PutAsJsonAsync(
            ProfileUrl,
            new UpdateProfileRequest { Phone = "+380 (44) 123-45-67" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _database.LoadProfileAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .Phone.ShouldBe("+380 (44) 123-45-67");
    }

    [Theory]
    [InlineData("call me")]
    [InlineData("1234")]
    [InlineData("+49 30 1234567 ext")]
    [InlineData("1234567890123456789012345")]
    public async Task UpdateProfile_InvalidPhone_Returns400AndStoresNothing(string phone)
    {
        var response = await _client.PutAsJsonAsync(
            ProfileUrl,
            new UpdateProfileRequest { Phone = phone },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _database.LoadProfileAsync(UserId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task AddAddress_Valid_Returns201WithLocationOfAddress()
    {
        var response = await _client.PostAsJsonAsync(AddressesUrl, Request, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var address = await _client.GetFromJsonAsync<AddressResponse>(
            response.Headers.Location.ShouldNotBeNull(),
            _cancellationToken
        );
        address.ShouldNotBeNull().City.ShouldBe("Berlin");
    }

    [Fact]
    public async Task AddAddress_MissingCity_Returns400Problem()
    {
        var response = await _client.PostAsJsonAsync(
            AddressesUrl,
            new
            {
                recipientName = "Jane Doe",
                address = "1 Main St",
                postalCode = "10115",
                country = "Germany",
            },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task AddAddress_LimitReached_Returns409Problem()
    {
        for (var i = 0; i < CustomerRepository.MaxAddresses; i++)
        {
            await _database.SeedAddressAsync(
                CustomerApiDatabase.NewAddress(UserId),
                _cancellationToken
            );
        }

        var response = await _client.PostAsJsonAsync(AddressesUrl, Request, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task GetAddress_OtherUsersAddress_Returns404()
    {
        var others = await _database.SeedAddressAsync(
            CustomerApiDatabase.NewAddress("user-2"),
            _cancellationToken
        );

        var response = await _client.GetAsync(
            $"{AddressesUrl}/{others.AddressId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAddress_OwnAddress_Returns204AndDeletesIt()
    {
        var address = await _database.SeedAddressAsync(
            CustomerApiDatabase.NewAddress(UserId),
            _cancellationToken
        );

        var response = await _client.DeleteAsync(
            $"{AddressesUrl}/{address.AddressId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.LoadAddressesAsync(UserId, _cancellationToken)).ShouldBeEmpty();
    }
}
