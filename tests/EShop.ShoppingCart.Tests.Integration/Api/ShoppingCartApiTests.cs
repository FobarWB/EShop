using System.Net;
using System.Net.Http.Json;
using System.Text;
using EShop.Data.Entities;
using EShop.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace EShop.ShoppingCart.Tests.Integration.Api;

// Real HTTP requests through routing, model binding, [ApiController], JSON and PostgreSQL.
public class ShoppingCartApiTests : IClassFixture<ShoppingCartApiFactory>, IAsyncLifetime
{
    private const string UserId = "user-1";
    private const string ProblemJson = "application/problem+json";

    private readonly ShoppingCartDatabase _database;
    private readonly ShoppingCartApiFactory _factory;
    private readonly HttpClient _client;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public ShoppingCartApiTests(ShoppingCartDatabase database, ShoppingCartApiFactory factory)
    {
        _database = database;
        _factory = factory;
        _client = factory.CreateClient();
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
    public async Task CreateCart_Returns200AndStoresCart()
    {
        var response = await _client.PostAsJsonAsync(
            "api/carts/create",
            new { userId = UserId },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _database.LoadCartAsync(UserId, _cancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateCart_EmptyUserId_Returns400ValidationProblem()
    {
        // [Required] on Cart.UserId: [ApiController] rejects the request before the action runs.
        var response = await _client.PostAsJsonAsync(
            "api/carts/create",
            new { userId = "" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task AddItem_FirstItem_Returns200AndCreatesCart()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);

        var response = await AddItemAsync(productId, quantity: 2);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task AddItem_UnknownProduct_Returns404()
    {
        var response = await AddItemAsync(productId: 999, quantity: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddItem_NotEnoughStock_Returns409()
    {
        var productId = await _database.SeedProductAsync(1, _cancellationToken);

        var response = await AddItemAsync(productId, quantity: 2);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddItem_ZeroQuantity_Returns400()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);

        var response = await AddItemAsync(productId, quantity: 0);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddItem_EmptyBody_Returns400ValidationProblem()
    {
        // The action's own "item == null" check never runs: [ApiController] answers first.
        using var empty = new StringContent("", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(
            $"api/carts/{UserId}/add",
            empty,
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task GetCart_Existing_Returns200WithItems()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);
        await AddItemAsync(productId, quantity: 2);

        var cart = await _client.GetFromJsonAsync<Cart>($"api/carts/{UserId}", _cancellationToken);

        cart.ShouldNotBeNull().CartItems.ShouldHaveSingleItem().ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task GetCart_Unknown_Returns404()
    {
        var response = await _client.GetAsync($"api/carts/{UserId}", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateQuantity_WithinStock_Returns204AndUpdates()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);
        await AddItemAsync(productId, quantity: 1);

        // The body is a bare JSON number: [FromBody] int newQuantity.
        var response = await _client.PutAsJsonAsync(
            $"api/carts/{UserId}/update/{productId}",
            4,
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(4);
    }

    [Fact]
    public async Task UpdateQuantity_AboveStock_Returns400()
    {
        var productId = await _database.SeedProductAsync(2, _cancellationToken);
        await AddItemAsync(productId, quantity: 1);

        var response = await _client.PutAsJsonAsync(
            $"api/carts/{UserId}/update/{productId}",
            3,
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveItem_Returns204AndRemovesIt()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);
        await AddItemAsync(productId, quantity: 1);

        var response = await _client.DeleteAsync(
            $"api/carts/{UserId}/remove/{productId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClearCart_Returns200AndEmptiesCart()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);
        await AddItemAsync(productId, quantity: 1);

        var response = await _client.DeleteAsync($"api/carts/{UserId}/clear", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteCart_Existing_Returns200AndDeletesIt()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);
        await AddItemAsync(productId, quantity: 1);

        var response = await _client.DeleteAsync($"api/carts/{UserId}", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _database.LoadCartAsync(UserId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteCart_Unknown_Returns404()
    {
        var response = await _client.DeleteAsync($"api/carts/{UserId}", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task UpdateQuantity_NotPositive_Returns400AndKeepsQuantity(int newQuantity)
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);
        await AddItemAsync(productId, quantity: 1);

        var response = await _client.PutAsJsonAsync(
            $"api/carts/{UserId}/update/{productId}",
            newQuantity,
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(1);
    }

    [Theory]
    [InlineData("http://localhost:4200", true)]
    [InlineData("https://evil.example", false)]
    public async Task Preflight_AllowsOnlyConfiguredOrigin(string origin, bool allowed)
    {
        // Origins come from Cors:AllowedOrigins in appsettings.json.
        using var request = new HttpRequestMessage(HttpMethod.Options, $"api/carts/{UserId}");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request, _cancellationToken);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values);
        (values?.SingleOrDefault()).ShouldBe(allowed ? origin : null);
    }

    [Fact]
    public async Task Request_BodyIsNotLogged()
    {
        // Request bodies will carry passwords and tokens (#41): they must never reach the logs.
        // The marker is in a property the API ignores, so only a body logger could write it.
        const string Marker = "body-marker-40";
        var logs = new CapturingLoggerProvider();
        using var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(l => l.AddProvider(logs)))
            .CreateClient();

        await client.PostAsJsonAsync(
            "api/carts/create",
            new { userId = UserId, note = Marker },
            _cancellationToken
        );

        logs.Messages.ShouldNotBeEmpty();
        logs.Messages.ShouldNotContain(m => m.Contains(Marker));
    }

    private Task<HttpResponseMessage> AddItemAsync(int productId, int quantity) =>
        _client.PostAsJsonAsync(
            $"api/carts/{UserId}/add",
            new { productId, quantity },
            _cancellationToken
        );
}
