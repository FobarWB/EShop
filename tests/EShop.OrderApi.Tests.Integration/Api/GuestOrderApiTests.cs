using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EShop.Data.Entities;
using EShop.OrderApi.Contracts;
using EShop.OrderApi.Controllers;
using EShop.Tests.Common;

namespace EShop.OrderApi.Tests.Integration.Api;

// POST api/orders/guest through routing, [ApiController] validation, JSON and PostgreSQL; Keycloak is the fake.
public class GuestOrderApiTests : IClassFixture<OrderApiFactory>, IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";
    private const string GuestEmail = "jane@example.com";

    private static readonly object Address = new
    {
        shipName = "Jane Doe",
        shipAddress = "1 Main St",
        shipCity = "Berlin",
        shipPostalCode = "10115",
        shipCountry = "Germany",
    };

    private readonly OrderApiDatabase _database;
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _guest;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public GuestOrderApiTests(OrderApiDatabase database, OrderApiFactory factory)
    {
        _database = database;
        _factory = factory;
        // No AsUser: the visitor is not signed in.
        _guest = factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        _factory.Accounts.Reset();
        await _database.ResetAsync(_cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _guest.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PlaceGuestOrder_NewEmail_Returns201AndStoresGuestOrder()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);

        var response = await PlaceGuestOrderAsync(
            Guid.NewGuid(),
            Body(" Jane@Example.COM ", (chai.ProductId, 2))
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = (await _database.LoadOrdersAsync(_cancellationToken)).ShouldHaveSingleItem();
        order.UserId.ShouldBeNull();
        order.PlacedAsGuest.ShouldBeTrue();
        order.CustomerEmail.ShouldBe(GuestEmail);
        order.OrderDetails.ShouldHaveSingleItem().Quantity.ShouldBe((short)2);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)8);
    }

    [Fact]
    public async Task PlaceGuestOrder_NewEmail_StoresOrderPlacedAndGuestOrderPlaced()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);

        await PlaceGuestOrderAsync(Guid.NewGuid(), Body(GuestEmail, (chai.ProductId, 1)));

        var order = (await _database.LoadOrdersAsync(_cancellationToken)).ShouldHaveSingleItem();
        var outbox = await _database.LoadOutboxAsync(_cancellationToken);
        outbox
            .Select(m => m.Type)
            .OrderBy(t => t)
            .ShouldBe([OrderEventTypes.GuestOrderPlaced, OrderEventTypes.OrderPlaced]);
        PayloadOf<OrderPlaced>(outbox.Single(m => m.Type == OrderEventTypes.OrderPlaced))
            .PlacedAsGuest.ShouldBeTrue();
        var guestPlaced = PayloadOf<GuestOrderPlaced>(
            outbox.Single(m => m.Type == OrderEventTypes.GuestOrderPlaced)
        );
        guestPlaced.OrderId.ShouldBe(order.OrderId);
        guestPlaced.Email.ShouldBe(GuestEmail);
    }

    [Fact]
    public async Task PlaceGuestOrder_EmailHasAccount_Returns409AccountExistsAndStoresNothing()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        _factory.Accounts.Add(GuestEmail);

        var response = await PlaceGuestOrderAsync(
            Guid.NewGuid(),
            Body("JANE@example.com", (chai.ProductId, 1))
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await ProblemCodeAsync(response)).ShouldBe("account-exists");
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldBeEmpty();
        (await _database.LoadOutboxAsync(_cancellationToken)).ShouldBeEmpty();
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)10);
    }

    [Fact]
    public async Task PlaceGuestOrder_SameKeyAfterAccountCreated_ReturnsTheSameOrder()
    {
        // A lost response: the account is created meanwhile, and the retry must still get its order, not 409.
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        var key = Guid.NewGuid();
        var first = await PlaceGuestOrderAsync(key, Body(GuestEmail, (chai.ProductId, 1)));
        _factory.Accounts.Add(GuestEmail);

        var retry = await PlaceGuestOrderAsync(key, Body(GuestEmail, (chai.ProductId, 1)));

        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await OrderIdAsync(retry)).ShouldBe(await OrderIdAsync(first));
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PlaceGuestOrder_UnknownProduct_Returns409WithProductIds()
    {
        var response = await PlaceGuestOrderAsync(Guid.NewGuid(), Body(GuestEmail, (999, 1)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProductIdsAsync(response)).ShouldBe([999]);
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PlaceGuestOrder_AboveStock_Returns409WithProductIds()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 2, _cancellationToken);

        var response = await PlaceGuestOrderAsync(
            Guid.NewGuid(),
            Body(GuestEmail, (chai.ProductId, 3))
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProductIdsAsync(response)).ShouldBe([chai.ProductId]);
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PlaceGuestOrder_KeycloakUnavailable_Returns503AndStoresNothing()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        _factory.Accounts.Unavailable = true;

        var response = await PlaceGuestOrderAsync(
            Guid.NewGuid(),
            Body(GuestEmail, (chai.ProductId, 1))
        );

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldBeEmpty();
    }

    // [ApiController] answers before the action runs, also for the nested address and the list items.
    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task PlaceGuestOrder_InvalidBody_Returns400(string name, object body)
    {
        var response = await PlaceGuestOrderAsync(Guid.NewGuid(), body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, name);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    public static TheoryData<string, object> InvalidBodies() =>
        new()
        {
            {
                "no items",
                new
                {
                    email = GuestEmail,
                    address = Address,
                    items = Array.Empty<object>(),
                }
            },
            { "invalid email", Body("not-an-email", (1, 1)) },
            { "quantity 0", Body(GuestEmail, (1, 0)) },
            {
                "no address",
                new { email = GuestEmail, items = new[] { new { productId = 1, quantity = 1 } } }
            },
        };

    [Fact]
    public async Task PlaceGuestOrder_WithoutIdempotencyKey_Returns400()
    {
        var response = await _guest.PostAsJsonAsync(
            "api/orders/guest",
            Body(GuestEmail, (1, 1)),
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static object Body(string email, params (int ProductId, int Quantity)[] items) =>
        new
        {
            email,
            address = Address,
            items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity }),
        };

    private Task<HttpResponseMessage> PlaceGuestOrderAsync(Guid key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/orders/guest")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add(OrdersController.IdempotencyKeyHeader, key.ToString());
        return _guest.SendAsync(request, _cancellationToken);
    }

    // Only the id: the status is sent as a name, which the default JSON options cannot read into the enum.
    private async Task<int> OrderIdAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(_cancellationToken)
        );
        return json.RootElement.GetProperty("orderId").GetInt32();
    }

    private async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(_cancellationToken)
        );
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private async Task<int[]> ProductIdsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(_cancellationToken)
        );
        return json
            .RootElement.GetProperty("productIds")
            .EnumerateArray()
            .Select(e => e.GetInt32())
            .ToArray();
    }

    // The outbox payload as a consumer reads it.
    private static T PayloadOf<T>(OutboxMessage message)
        where T : class =>
        JsonSerializer.Deserialize<T>(message.Payload, JsonSerializerOptions.Web).ShouldNotBeNull();
}
