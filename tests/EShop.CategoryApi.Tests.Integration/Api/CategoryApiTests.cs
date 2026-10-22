using System.Net;
using System.Net.Http.Json;
using EShop.Data.Entities;

namespace EShop.CategoryApi.Tests.Integration.Api;

// Real HTTP requests through routing, model binding, [ApiController], JSON and PostgreSQL.
public class CategoryApiTests : IClassFixture<CategoryApiFactory>, IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private readonly CategoryApiDatabase _database;
    private readonly HttpClient _client;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public CategoryApiTests(CategoryApiDatabase database, CategoryApiFactory factory)
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

        var category = await _client.GetFromJsonAsync<Category>(
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
        var response = await _client.PostAsJsonAsync(
            "api/categories",
            new { categoryName = "Beverages" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (
            await response.Content.ReadFromJsonAsync<Category>(_cancellationToken)
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
        var response = await _client.PostAsJsonAsync(
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
        var response = await _client.PostAsJsonAsync(
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
        // The controller loads the category before updating it with the request body:
        // this only works because RetrieveAsync does not track the entity.
        var beverages = await SeedCategoryAsync("Beverages");

        var response = await _client.PutAsJsonAsync(
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
    public async Task Update_IdMismatch_Returns400()
    {
        var beverages = await SeedCategoryAsync("Beverages");

        var response = await _client.PutAsJsonAsync(
            $"api/categories/{beverages.CategoryId}",
            new { categoryId = beverages.CategoryId + 1, categoryName = "Drinks" },
            _cancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Unknown_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
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

        var response = await _client.DeleteAsync(
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
        var response = await _client.DeleteAsync("api/categories/999", _cancellationToken);

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

        var response = await _client.DeleteAsync(
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

    private Task<Category> SeedCategoryAsync(string name) =>
        _database.SeedAsync(new Category { CategoryName = name }, _cancellationToken);

    private async Task<List<Category>> GetCategoriesAsync(string url) =>
        (await _client.GetFromJsonAsync<List<Category>>(url, _cancellationToken)).ShouldNotBeNull();
}
