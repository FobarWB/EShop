using System.Net;
using System.Net.Http.Json;
using EShop.Data;
using EShop.Data.Entities;
using EShop.ProductApi.Dtos;
using EShop.ServiceDefaults;
using EShop.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EShop.ProductApi.Tests.Integration.Api;

// Real HTTP requests through routing, model binding, [ApiController], JSON and PostgreSQL.
public class ProductApiTests : IClassFixture<ProductApiFactory>, IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private readonly ProductApiDatabase _database;
    private readonly ProductApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public ProductApiTests(ProductApiDatabase database, ProductApiFactory factory)
    {
        _database = database;
        _factory = factory;
        _client = factory.CreateClient();
        _admin = factory.CreateClient().AsAdmin();
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _admin.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetHealth_DatabaseAvailable_ReturnsOk()
    {
        var response = await _client.GetAsync("health", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProducts_ReturnsAllProducts()
    {
        await SeedProductAsync("Chai");
        await SeedProductAsync("Chang");

        var products = await GetProductsAsync("api/products");

        products.Select(p => p.ProductName).ShouldBe(["Chai", "Chang"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetProducts_WithCategory_ReturnsOnlyThatCategory()
    {
        var beverages = await _database.SeedAsync(
            new Category { CategoryName = "Beverages" },
            _cancellationToken
        );
        var condiments = await _database.SeedAsync(
            new Category { CategoryName = "Condiments" },
            _cancellationToken
        );
        await SeedProductAsync("Chai", beverages.CategoryId);
        await SeedProductAsync("Aniseed Syrup", condiments.CategoryId);

        var products = await GetProductsAsync($"api/products?categoryId={beverages.CategoryId}");

        products.ShouldHaveSingleItem().ProductName.ShouldBe("Chai");
    }

    [Fact]
    public async Task GetProducts_SeesChangesMadeOutsideTheApi()
    {
        // Regression test for the removed static cache: rows added directly to PostgreSQL must be visible.
        (await GetProductsAsync("api/products")).ShouldBeEmpty();

        await SeedProductAsync("Chai");

        (await GetProductsAsync("api/products"))
            .ShouldHaveSingleItem()
            .ProductName.ShouldBe("Chai");
    }

    [Fact]
    public async Task GetProducts_Paged_ReturnsThatPageInIdOrderAndTotalCount()
    {
        await SeedProductAsync("Chai");
        await SeedProductAsync("Chang");
        await SeedProductAsync("Aniseed Syrup");

        var first = await GetProductPageAsync("api/products?pageSize=2");
        var second = await GetProductPageAsync("api/products?page=2&pageSize=2");

        first.Items.Select(p => p.ProductName).ShouldBe(["Chai", "Chang"]);
        first.TotalCount.ShouldBe(3);
        second.Page.ShouldBe(2);
        second.Items.ShouldHaveSingleItem().ProductName.ShouldBe("Aniseed Syrup");
    }

    [Fact]
    public async Task GetProducts_WithoutPaging_ReturnsFirst20()
    {
        for (var i = 1; i <= 21; i++)
        {
            await SeedProductAsync($"Product {i}");
        }

        var page = await GetProductPageAsync("api/products");

        page.Items.Count.ShouldBe(20);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(20);
        page.TotalCount.ShouldBe(21);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task GetProducts_InvalidPaging_Returns400ValidationProblem(string query)
    {
        var response = await _client.GetAsync($"api/products?{query}", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task GetProduct_Existing_Returns200WithProduct()
    {
        var chai = await SeedProductAsync("Chai");

        var product = await _client.GetFromJsonAsync<ProductResponse>(
            $"api/products/{chai.ProductId}",
            _cancellationToken
        );

        product.ShouldNotBeNull().ProductName.ShouldBe("Chai");
    }

    [Fact]
    public async Task GetProduct_Unknown_Returns404()
    {
        var response = await _client.GetAsync("api/products/999", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Returns201WithLocationAndStoresProduct()
    {
        var response = await _admin.PostAsJsonAsync(
            "api/products",
            new { productName = "Chai" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (
            await response.Content.ReadFromJsonAsync<ProductResponse>(_cancellationToken)
        ).ShouldNotBeNull();
        response
            .Headers.Location.ShouldNotBeNull()
            .AbsolutePath.ShouldBe($"/api/products/{created.ProductId}");
        (
            await _database.FindAsync<Product>(created.ProductId, _cancellationToken)
        ).ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_WithoutName_Returns400ValidationProblem()
    {
        // ProductName is a non-nullable string, so MVC treats it as [Required].
        var response = await _admin.PostAsJsonAsync(
            "api/products",
            new { unitPrice = 18 },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task Create_NameTooLong_Returns400ValidationProblem()
    {
        // [StringLength(40)] is checked by [ApiController] before PostgreSQL would reject varchar(40).
        var response = await _admin.PostAsJsonAsync(
            "api/products",
            new { productName = new string('x', 41) },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task Update_Existing_Returns204AndStoresChanges()
    {
        // Only the columns in the request change; the id comes from the route.
        var chai = await SeedProductAsync("Chai");

        var response = await _admin.PutAsJsonAsync(
            $"api/products/{chai.ProductId}",
            new { productId = chai.ProductId, productName = "Chai Latte" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .ProductName.ShouldBe("Chai Latte");
    }

    [Fact]
    public async Task Update_Unknown_Returns404()
    {
        var response = await _admin.PutAsJsonAsync(
            "api/products/999",
            new { productId = 999, productName = "Chai" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_IdInBodyIsIgnored()
    {
        // The id comes from the route only.
        var chai = await SeedProductAsync("Chai");

        var response = await _admin.PutAsJsonAsync(
            $"api/products/{chai.ProductId}",
            new { productId = chai.ProductId + 1, productName = "Chai Latte" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .ProductName.ShouldBe("Chai Latte");
    }

    [Fact]
    public async Task Update_WithNestedCategory_DoesNotStoreIt()
    {
        // Overposting: the request body has no related data, so a client cannot insert rows through it.
        var chai = await SeedProductAsync("Chai");

        await _admin.PutAsJsonAsync(
            $"api/products/{chai.ProductId}",
            new
            {
                productId = chai.ProductId,
                productName = "Chai",
                category = new { categoryName = "Injected" },
            },
            _cancellationToken
        );

        await using var db = _database.CreateContext();
        (await db.Categories.AnyAsync(_cancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Update_UnitsInStockNull_Returns400ValidationProblem()
    {
        // UnitsInStock is a non-nullable short (#31): null in JSON fails model binding.
        var chai = await SeedProductAsync("Chai");

        var response = await _admin.PutAsJsonAsync(
            $"api/products/{chai.ProductId}",
            new
            {
                productId = chai.ProductId,
                productName = "Chai",
                unitsInStock = (short?)null,
            },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task Update_WithoutUnitsInStock_StoresZero()
    {
        var chai = await _database.SeedAsync(
            new Product { ProductName = "Chai", UnitsInStock = 39 },
            _cancellationToken
        );

        await _admin.PutAsJsonAsync(
            $"api/products/{chai.ProductId}",
            new { productId = chai.ProductId, productName = "Chai" },
            _cancellationToken
        );

        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)0);
    }

    [Fact]
    public async Task Delete_Existing_Returns204AndDeletesIt()
    {
        var chai = await SeedProductAsync("Chai");

        var response = await _admin.DeleteAsync(
            $"api/products/{chai.ProductId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        var response = await _admin.DeleteAsync("api/products/999", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ProductInCart_Returns409AndKeepsProduct()
    {
        var chai = await SeedProductAsync("Chai");
        await _database.SeedAsync(
            new Cart
            {
                UserId = "user-1",
                CartItems = [new CartItem { ProductId = chai.ProductId, Quantity = 1 }],
            },
            _cancellationToken
        );

        var response = await _admin.DeleteAsync(
            $"api/products/{chai.ProductId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_ProductInOrder_Returns409AndKeepsProduct()
    {
        var chai = await SeedProductAsync("Chai");
        await _database.SeedAsync(
            new Order
            {
                OrderDetails =
                [
                    new OrderDetail
                    {
                        ProductId = chai.ProductId,
                        UnitPrice = 18,
                        Quantity = 1,
                    },
                ],
            },
            _cancellationToken
        );

        var response = await _admin.DeleteAsync(
            $"api/products/{chai.ProductId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_ProductAddedToCartAfterCheck_Returns409AndKeepsProduct()
    {
        // The race from #32: another request puts the product into a cart after the in-use check;
        // PostgreSQL rejects the DELETE (23503) and the exception handler turns it into 409.
        var chai = await SeedProductAsync("Chai");
        using var admin = CreateAdminWithConcurrentWrite(() =>
            _database.SeedAsync(
                new Cart
                {
                    UserId = "user-1",
                    CartItems = [new CartItem { ProductId = chai.ProductId, Quantity = 1 }],
                },
                _cancellationToken
            )
        );

        var response = await admin.DeleteAsync(
            $"api/products/{chai.ProductId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("POST", "api/products")]
    [InlineData("PUT", "api/products/1")]
    [InlineData("DELETE", "api/products/1")]
    public async Task WriteEndpoints_Anonymous_Return401(string method, string url)
    {
        var response = await SendJsonAsync(_client, method, url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST", "api/products")]
    [InlineData("PUT", "api/products/1")]
    [InlineData("DELETE", "api/products/1")]
    public async Task WriteEndpoints_WithoutAdminRole_Return403(string method, string url)
    {
        using var user = _factory.CreateClient().AsUser("user-1");

        var response = await SendJsonAsync(user, method, url);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOpenApiDocument_Anonymous_Returns200()
    {
        var response = await _client.GetAsync("openapi/v1.json", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_AuthorizationHeaderIsNotLogged()
    {
        // HttpLogging writes request headers; a bearer token in the logs could be replayed by anyone who reads them.
        const string Marker = "token-marker-41";
        var logs = new CapturingLoggerProvider();
        using var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(l => l.AddProvider(logs)))
            .CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Marker);

        await client.GetAsync("api/products", _cancellationToken);

        logs.Messages.ShouldContain(m => m.Contains("Authorization"));
        logs.Messages.ShouldNotContain(m => m.Contains(Marker));
    }

    [Theory]
    [InlineData("http://localhost:4200", true)]
    [InlineData("https://evil.example", false)]
    public async Task Preflight_AllowsOnlyConfiguredOrigin(string origin, bool allowed)
    {
        // Origins come from Cors:AllowedOrigins in appsettings.json.
        using var request = new HttpRequestMessage(HttpMethod.Options, "api/products");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request, _cancellationToken);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values);
        (values?.SingleOrDefault()).ShouldBe(allowed ? origin : null);
    }

    private Task<Product> SeedProductAsync(string name, int? categoryId = null) =>
        _database.SeedAsync(
            new Product { ProductName = name, CategoryId = categoryId },
            _cancellationToken
        );

    private HttpClient CreateAdminWithConcurrentWrite(Func<Task> write) =>
        _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.ConfigureDbContext<EShopDbContext>(options =>
                        options.AddInterceptors(new BeforeSaveChangesInterceptor(write))
                    )
                )
            )
            .CreateClient()
            .AsAdmin();

    private async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(string url) =>
        (await GetProductPageAsync(url)).Items;

    private async Task<PagedResponse<ProductResponse>> GetProductPageAsync(string url) =>
        (
            await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(url, _cancellationToken)
        ).ShouldNotBeNull();

    private Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string method, string url) =>
        client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = JsonContent.Create(new { }),
            },
            _cancellationToken
        );
}
