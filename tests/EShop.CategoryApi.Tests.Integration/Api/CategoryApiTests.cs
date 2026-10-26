using System.Net;
using System.Net.Http.Json;
using EShop.CategoryApi.Dtos;
using EShop.Data;
using EShop.Data.Entities;
using EShop.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EShop.CategoryApi.Tests.Integration.Api;

// Real HTTP requests through routing, model binding, [ApiController], JSON and PostgreSQL.
public class CategoryApiTests : IClassFixture<CategoryApiFactory>, IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private readonly CategoryApiDatabase _database;
    private readonly CategoryApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public CategoryApiTests(CategoryApiDatabase database, CategoryApiFactory factory)
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
    public async Task GetCategories_ReturnsAllCategories()
    {
        await SeedCategoryAsync("Beverages");
        await SeedCategoryAsync("Condiments");

        var categories = await GetCategoriesAsync("api/categories");

        categories
            .Select(c => c.CategoryName)
            .ShouldBe(["Beverages", "Condiments"], ignoreOrder: true);
    }

    // Seeded: Beverages, Condiments, Confections. Case-insensitive "contains".
    [Theory]
    [InlineData("con", 2)]
    [InlineData("BEV", 1)]
    [InlineData("xyz", 0)]
    public async Task GetCategories_ByName_FiltersCaseInsensitively(string name, int expectedCount)
    {
        await SeedCategoryAsync("Beverages");
        await SeedCategoryAsync("Condiments");
        await SeedCategoryAsync("Confections");

        var categories = await GetCategoriesAsync($"api/categories?categoryName={name}");

        categories.Count.ShouldBe(expectedCount);
    }

    [Fact]
    public async Task GetCategories_NameWithWildcard_MatchesLiterally()
    {
        // % is a LIKE wildcard: unescaped, it would match every category.
        await SeedCategoryAsync("Beverages");
        await SeedCategoryAsync("Condiments");

        var categories = await GetCategoriesAsync("api/categories?categoryName=%25");

        categories.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCategories_SeesChangesMadeOutsideTheApi()
    {
        // Regression test for the removed static cache: rows added directly to PostgreSQL must be visible.
        (await GetCategoriesAsync("api/categories")).ShouldBeEmpty();

        await SeedCategoryAsync("Beverages");

        (await GetCategoriesAsync("api/categories"))
            .ShouldHaveSingleItem()
            .CategoryName.ShouldBe("Beverages");
    }

    [Fact]
    public async Task GetCategory_Existing_Returns200WithCategory()
    {
        var beverages = await SeedCategoryAsync("Beverages");

        var category = await _client.GetFromJsonAsync<CategoryResponse>(
            $"api/categories/{beverages.CategoryId}",
            _cancellationToken
        );

        category.ShouldNotBeNull().CategoryName.ShouldBe("Beverages");
    }

    [Fact]
    public async Task GetCategory_Unknown_Returns404()
    {
        var response = await _client.GetAsync("api/categories/999", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Returns201WithLocationAndStoresCategory()
    {
        var response = await _admin.PostAsJsonAsync(
            "api/categories",
            new { categoryName = "Beverages" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (
            await response.Content.ReadFromJsonAsync<CategoryResponse>(_cancellationToken)
        ).ShouldNotBeNull();
        response
            .Headers.Location.ShouldNotBeNull()
            .AbsolutePath.ShouldBe($"/api/categories/{created.CategoryId}");
        (
            await _database.FindAsync<Category>(created.CategoryId, _cancellationToken)
        ).ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_WithoutName_Returns400ValidationProblem()
    {
        // CategoryName is a non-nullable string, so MVC treats it as [Required].
        var response = await _admin.PostAsJsonAsync(
            "api/categories",
            new { description = "Soft drinks" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task Create_NameTooLong_Returns400ValidationProblem()
    {
        // [StringLength(15)] is checked by [ApiController] before PostgreSQL would reject varchar(15).
        var response = await _admin.PostAsJsonAsync(
            "api/categories",
            new { categoryName = new string('x', 16) },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
    }

    [Fact]
    public async Task Update_Existing_Returns204AndStoresChanges()
    {
        // Only the columns in the request change; the id comes from the route.
        var beverages = await SeedCategoryAsync("Beverages");

        var response = await _admin.PutAsJsonAsync(
            $"api/categories/{beverages.CategoryId}",
            new { categoryId = beverages.CategoryId, categoryName = "Drinks" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.FindAsync<Category>(beverages.CategoryId, _cancellationToken))
            .ShouldNotBeNull()
            .CategoryName.ShouldBe("Drinks");
    }

    [Fact]
    public async Task Update_IdInBodyIsIgnored()
    {
        // The id comes from the route only.
        var beverages = await SeedCategoryAsync("Beverages");

        var response = await _admin.PutAsJsonAsync(
            $"api/categories/{beverages.CategoryId}",
            new { categoryId = beverages.CategoryId + 1, categoryName = "Drinks" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _database.FindAsync<Category>(beverages.CategoryId, _cancellationToken))
            .ShouldNotBeNull()
            .CategoryName.ShouldBe("Drinks");
    }

    [Fact]
    public async Task Update_KeepsPicture()
    {
        // The request has no picture: an update must not overwrite the stored one with NULL.
        var beverages = await _database.SeedAsync(
            new Category { CategoryName = "Beverages", Picture = [1, 2, 3] },
            _cancellationToken
        );

        await _admin.PutAsJsonAsync(
            $"api/categories/{beverages.CategoryId}",
            new { categoryId = beverages.CategoryId, categoryName = "Drinks" },
            _cancellationToken
        );

        var stored = (
            await _database.FindAsync<Category>(beverages.CategoryId, _cancellationToken)
        ).ShouldNotBeNull();
        stored.CategoryName.ShouldBe("Drinks");
        stored.Picture.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Update_Unknown_Returns404()
    {
        var response = await _admin.PutAsJsonAsync(
            "api/categories/999",
            new { categoryId = 999, categoryName = "Drinks" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Existing_Returns204AndDeletesIt()
    {
        var beverages = await SeedCategoryAsync("Beverages");

        var response = await _admin.DeleteAsync(
            $"api/categories/{beverages.CategoryId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (
            await _database.FindAsync<Category>(beverages.CategoryId, _cancellationToken)
        ).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        var response = await _admin.DeleteAsync("api/categories/999", _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_CategoryWithProducts_Returns409AndKeepsCategory()
    {
        var beverages = await SeedCategoryAsync("Beverages");
        await _database.SeedAsync(
            new Product { ProductName = "Chai", CategoryId = beverages.CategoryId },
            _cancellationToken
        );

        var response = await _admin.DeleteAsync(
            $"api/categories/{beverages.CategoryId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (
            await _database.FindAsync<Category>(beverages.CategoryId, _cancellationToken)
        ).ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_CategoryGetsProductAfterCheck_Returns409AndKeepsCategory()
    {
        // The race from #32: another request adds a product to the category after the in-use check;
        // PostgreSQL rejects the DELETE (23503) and the exception handler turns it into 409.
        var beverages = await SeedCategoryAsync("Beverages");
        using var admin = CreateAdminWithConcurrentWrite(() =>
            _database.SeedAsync(
                new Product { ProductName = "Chai", CategoryId = beverages.CategoryId },
                _cancellationToken
            )
        );

        var response = await admin.DeleteAsync(
            $"api/categories/{beverages.CategoryId}",
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ProblemJson);
        (
            await _database.FindAsync<Category>(beverages.CategoryId, _cancellationToken)
        ).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("POST", "api/categories")]
    [InlineData("PUT", "api/categories/1")]
    [InlineData("DELETE", "api/categories/1")]
    public async Task WriteEndpoints_Anonymous_Return401(string method, string url)
    {
        var response = await SendJsonAsync(_client, method, url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST", "api/categories")]
    [InlineData("PUT", "api/categories/1")]
    [InlineData("DELETE", "api/categories/1")]
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

        await client.GetAsync("api/categories", _cancellationToken);

        logs.Messages.ShouldContain(m => m.Contains("Authorization"));
        logs.Messages.ShouldNotContain(m => m.Contains(Marker));
    }

    [Theory]
    [InlineData("http://localhost:4200", true)]
    [InlineData("https://evil.example", false)]
    public async Task Preflight_AllowsOnlyConfiguredOrigin(string origin, bool allowed)
    {
        // Origins come from Cors:AllowedOrigins in appsettings.json.
        using var request = new HttpRequestMessage(HttpMethod.Options, "api/categories");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request, _cancellationToken);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values);
        (values?.SingleOrDefault()).ShouldBe(allowed ? origin : null);
    }

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

    private Task<Category> SeedCategoryAsync(string name) =>
        _database.SeedAsync(new Category { CategoryName = name }, _cancellationToken);

    private async Task<List<CategoryResponse>> GetCategoriesAsync(string url) =>
        (
            await _client.GetFromJsonAsync<List<CategoryResponse>>(url, _cancellationToken)
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
