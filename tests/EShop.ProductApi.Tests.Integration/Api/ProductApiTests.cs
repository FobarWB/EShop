using System.Net;
using System.Net.Http.Json;
using EShop.Data.Entities;

namespace EShop.ProductApi.Tests.Integration.Api;

// Real HTTP requests through routing, model binding, [ApiController], JSON and PostgreSQL.
public class ProductApiTests : IClassFixture<ProductApiFactory>, IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private readonly ProductApiDatabase _database;
    private readonly HttpClient _client;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public ProductApiTests(ProductApiDatabase database, ProductApiFactory factory)
    {
        _database = database;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
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
    public async Task GetProduct_Existing_Returns200WithProduct()
    {
        var chai = await SeedProductAsync("Chai");

        var product = await _client.GetFromJsonAsync<Product>(
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
        var response = await _client.PostAsJsonAsync(
            "api/products",
            new { productName = "Chai" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (
            await response.Content.ReadFromJsonAsync<Product>(_cancellationToken)
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
        var response = await _client.PostAsJsonAsync(
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
        var response = await _client.PostAsJsonAsync(
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
        // The controller loads the product before updating it with the request body:
        // this only works because RetrieveAsync does not track the entity.
        var chai = await SeedProductAsync("Chai");

        var response = await _client.PutAsJsonAsync(
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
    public async Task Update_IdMismatch_Returns400()
    {
        var chai = await SeedProductAsync("Chai");

        var response = await _client.PutAsJsonAsync(
            $"api/products/{chai.ProductId}",
            new { productId = chai.ProductId + 1, productName = "Chai" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Unknown_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            "api/products/999",
            new { productId = 999, productName = "Chai" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_UnitsInStockNull_Returns400ValidationProblem()
    {
        // UnitsInStock is a non-nullable short (#31): null in JSON fails model binding.
        var chai = await SeedProductAsync("Chai");

        var response = await _client.PutAsJsonAsync(
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

        await _client.PutAsJsonAsync(
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

        var response = await _client.DeleteAsync(
            $"api/products/{chai.ProductId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        var response = await _client.DeleteAsync("api/products/999", _cancellationToken);

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

        var response = await _client.DeleteAsync(
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

        var response = await _client.DeleteAsync(
            $"api/products/{chai.ProductId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken)).ShouldNotBeNull();
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

    private async Task<List<Product>> GetProductsAsync(string url) =>
        (await _client.GetFromJsonAsync<List<Product>>(url, _cancellationToken)).ShouldNotBeNull();
}
