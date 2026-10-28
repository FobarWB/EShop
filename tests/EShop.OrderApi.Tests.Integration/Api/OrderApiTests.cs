using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EShop.Data;
using EShop.Data.Entities;
using EShop.OrderApi.Controllers;
using EShop.OrderApi.Dtos;
using EShop.Tests.Common;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.OrderApi.Tests.Integration.Api;

// Real HTTP requests through routing, model binding, [ApiController], JSON and PostgreSQL.
public class OrderApiTests : IClassFixture<OrderApiFactory>, IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";
    private const string UserId = "user-1";

    private static readonly object Address = new
    {
        shipName = "Jane Doe",
        shipAddress = "1 Main St",
        shipCity = "Berlin",
        shipPostalCode = "10115",
        shipCountry = "Germany",
    };

    // The API's JSON settings: enums as names ("status": "Cancelled").
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly OrderApiDatabase _database;
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _user;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public OrderApiTests(OrderApiDatabase database, OrderApiFactory factory)
    {
        _database = database;
        _factory = factory;
        _user = factory.CreateClient().AsUser(UserId);
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync()
    {
        _user.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetHealth_DatabaseAvailable_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("health", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlaceOrder_CartWithItems_Returns201WithLocationOfTheOrder()
    {
        var chai = await SeedCartAsync(UserId, unitsInStock: 10);

        var response = await PlaceOrderAsync(_user, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = (
            await response.Content.ReadFromJsonAsync<OrderResponse>(Json, _cancellationToken)
        ).ShouldNotBeNull();
        order.Items.ShouldHaveSingleItem().ProductId.ShouldBe(chai.ProductId);
        var location = response.Headers.Location.ShouldNotBeNull();
        (await _user.GetFromJsonAsync<OrderResponse>(location, Json, _cancellationToken))
            .ShouldNotBeNull()
            .OrderId.ShouldBe(order.OrderId);
    }

    [Fact]
    public async Task PlaceOrder_WithoutIdempotencyKey_Returns400AndPlacesNoOrder()
    {
        await SeedCartAsync(UserId, unitsInStock: 10);

        var response = await _user.PostAsJsonAsync("api/orders", Address, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PlaceOrder_MalformedIdempotencyKey_Returns400()
    {
        await SeedCartAsync(UserId, unitsInStock: 10);

        var response = await PlaceOrderAsync(_user, "not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlaceOrder_WithoutCity_Returns400ValidationProblem()
    {
        await SeedCartAsync(UserId, unitsInStock: 10);

        var response = await PlaceOrderAsync(
            _user,
            Guid.NewGuid().ToString(),
            new
            {
                shipName = "Jane Doe",
                shipAddress = "1 Main St",
                shipPostalCode = "10115",
                shipCountry = "Germany",
            }
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task PlaceOrder_Anonymous_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await PlaceOrderAsync(anonymous, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlaceOrder_InsufficientStock_Returns409WithProductIds()
    {
        var chai = await SeedCartAsync(UserId, unitsInStock: 1, quantity: 2);

        var response = await PlaceOrderAsync(_user, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_cancellationToken);
        problem
            .GetProperty("productIds")
            .EnumerateArray()
            .Select(id => id.GetInt32())
            .ShouldBe([chai.ProductId]);
    }

    [Fact]
    public async Task PlaceOrder_Retried_ReturnsSameOrder()
    {
        await SeedCartAsync(UserId, unitsInStock: 10);
        var key = Guid.NewGuid();
        var first = await PlaceOrderAsync(_user, key);

        var retry = await PlaceOrderAsync(_user, key);

        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await retry.Content.ReadFromJsonAsync<OrderResponse>(Json, _cancellationToken))
            .ShouldNotBeNull()
            .OrderId.ShouldBe(
                (await first.Content.ReadFromJsonAsync<OrderResponse>(Json, _cancellationToken))
                    .ShouldNotBeNull()
                    .OrderId
            );
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PlaceOrder_LastUnitBoughtByAnotherBuyer_Returns409AndKeepsTheirOrder()
    {
        // Both buyers have the last unit in their cart. While buyer 1 saves, buyer 2 completes a whole checkout:
        // buyer 1's stock update finds a changed xmin, and the exception handler answers 409.
        var chai = await SeedCartAsync(UserId, unitsInStock: 1);
        await _database.SeedCartAsync("user-2", chai.ProductId, 1, _cancellationToken);
        using var buyer1 = CreateClientWithConcurrentWrite(async () =>
        {
            using var buyer2 = _factory.CreateClient().AsUser("user-2");
            var response = await PlaceOrderAsync(buyer2, Guid.NewGuid());
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        });

        var response = await PlaceOrderAsync(buyer1.AsUser(UserId), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.LoadOrdersAsync(_cancellationToken))
            .ShouldHaveSingleItem()
            .UserId.ShouldBe("user-2");
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)0);
    }

    [Fact]
    public async Task GetOrder_OtherUsersOrder_Returns404()
    {
        var order = await _database.SeedAsync(
            OrderApiDatabase.NewOrder("user-2", Guid.NewGuid()),
            _cancellationToken
        );

        var response = await _user.GetAsync($"api/orders/{order.OrderId}", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrders_ReturnsOwnOrdersWithStatusAsName()
    {
        await _database.SeedOrderAsync(UserId, OrderStatus.Paid, _cancellationToken);
        await _database.SeedOrderAsync("user-2", OrderStatus.Pending, _cancellationToken);

        var page = await _user.GetFromJsonAsync<JsonElement>(
            "api/orders?page=1&pageSize=10",
            _cancellationToken
        );

        page.GetProperty("totalCount").GetInt32().ShouldBe(1);
        page.GetProperty("items")[0].GetProperty("status").GetString().ShouldBe("Paid");
    }

    [Fact]
    public async Task GetOrders_PageSizeAboveMaximum_Returns400()
    {
        var response = await _user.GetAsync("api/orders?pageSize=101", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelOrder_Pending_Returns200WithCancelledOrderAndReturnsStock()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Pending,
            _cancellationToken,
            (chai.ProductId, 2)
        );

        var response = await _user.PostAsync(
            $"api/orders/{seeded.OrderId}/cancel",
            null,
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<OrderResponse>(Json, _cancellationToken))
            .ShouldNotBeNull()
            .Status.ShouldBe(OrderStatus.Cancelled);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)12);
    }

    [Fact]
    public async Task CancelOrder_Shipped_Returns409()
    {
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Shipped,
            _cancellationToken
        );

        var response = await _user.PostAsync(
            $"api/orders/{seeded.OrderId}/cancel",
            null,
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task ChangeStatus_Customer_Returns403()
    {
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Pending,
            _cancellationToken
        );

        var response = await PutStatusAsync(_user, seeded.OrderId, new { status = "Paid" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("""{ "status": "Lost" }""")]
    [InlineData("{}")]
    public async Task ChangeStatus_UnknownOrMissingStatus_Returns400(string body)
    {
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Pending,
            _cancellationToken
        );
        using var admin = _factory.CreateClient().AsAdmin();

        var response = await admin.PutAsync(
            $"api/orders/{seeded.OrderId}/status",
            new StringContent(body, Encoding.UTF8, "application/json"),
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task ChangeStatus_AdminWalksTheLifecycle_DeliversTheOrder()
    {
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Pending,
            _cancellationToken
        );
        using var admin = _factory.CreateClient().AsAdmin();

        foreach (var status in new[] { "Paid", "Shipped", "Delivered" })
        {
            (await PutStatusAsync(admin, seeded.OrderId, new { status })).StatusCode.ShouldBe(
                HttpStatusCode.NoContent
            );
        }

        var order = await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken);
        order.Status.ShouldBe(OrderStatus.Delivered);
        order.ShippedDate.ShouldNotBeNull();
        order.StatusChanges.ShouldAllBe(c => c.ChangedBy == "admin-1");
    }

    private Task<HttpResponseMessage> PutStatusAsync(HttpClient client, int orderId, object body) =>
        client.PutAsJsonAsync($"api/orders/{orderId}/status", body, _cancellationToken);

    private async Task<Product> SeedCartAsync(string userId, short unitsInStock, int quantity = 1)
    {
        var product = await _database.SeedProductAsync(
            "Chai",
            18m,
            unitsInStock,
            _cancellationToken
        );
        await _database.SeedCartAsync(userId, product.ProductId, quantity, _cancellationToken);
        return product;
    }

    private Task<HttpResponseMessage> PlaceOrderAsync(HttpClient client, Guid key) =>
        PlaceOrderAsync(client, key.ToString());

    private Task<HttpResponseMessage> PlaceOrderAsync(
        HttpClient client,
        string key,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/orders")
        {
            Content = JsonContent.Create(body ?? Address),
        };
        request.Headers.Add(OrdersController.IdempotencyKeyHeader, key);
        return client.SendAsync(request, _cancellationToken);
    }

    private HttpClient CreateClientWithConcurrentWrite(Func<Task> write) =>
        _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.ConfigureDbContext<EShopDbContext>(options =>
                        options.AddInterceptors(new BeforeSaveChangesInterceptor(write))
                    )
                )
            )
            .CreateClient();
}
