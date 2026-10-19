using EShop.CategoryApi.Controllers;
using EShop.CategoryApi.Repositories;
using EShop.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EShop.CategoryApi.Tests.Unit.Controllers;

public class CategoryApiControllerTests
{
    private const int CategoryId = 3;

    private readonly ICategoryRepository _repository = Substitute.For<ICategoryRepository>();
    private readonly CategoryAPIController _controller;

    public CategoryApiControllerTests()
    {
        _controller = new CategoryAPIController(_repository);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("  ", 3)]
    [InlineData("bev", 1)] // case-insensitive "contains": "Beverages"
    [InlineData("O", 2)] // "Condiments", "Confections"
    public async Task GetCategories_FiltersByName(string? categoryName, int expectedCount)
    {
        _repository
            .RetrieveAllAsync()
            .Returns(
                [Category(1, "Beverages"), Category(2, "Condiments"), Category(3, "Confections")]
            );

        var result = await _controller.GetCategories(categoryName);

        ValueOf<IEnumerable<Category>>(result.Result).Count().ShouldBe(expectedCount);
    }

    [Fact]
    public async Task GetCategory_Missing_Returns404()
    {
        _repository.RetrieveAsync(CategoryId).Returns((Category?)null);

        var result = await _controller.GetCategory(CategoryId);

        StatusCodeOf(result).ShouldBe(404);
    }

    [Fact]
    public async Task GetCategory_Exists_Returns200WithCategory()
    {
        var category = Category(CategoryId);
        _repository.RetrieveAsync(CategoryId).Returns(category);

        var result = await _controller.GetCategory(CategoryId);

        ValueOf<Category>(result).ShouldBe(category);
    }

    [Fact]
    public async Task Create_Created_Returns201WithRouteToNewCategory()
    {
        var category = Category(CategoryId);
        _repository.CreateAsync(category).Returns(category);

        var result = await _controller.Create(category);

        var created = result.ShouldBeOfType<CreatedAtRouteResult>();
        created.RouteName.ShouldBe(nameof(CategoryAPIController.GetCategory));
        created.RouteValues!["id"].ShouldBe(CategoryId);
    }

    [Fact]
    public async Task Create_RepositoryFailed_Returns400()
    {
        _repository.CreateAsync(Arg.Any<Category>()).Returns((Category?)null);

        var result = await _controller.Create(Category(CategoryId));

        StatusCodeOf(result).ShouldBe(400);
    }

    [Fact]
    public async Task Update_IdMismatch_Returns400WithoutUpdating()
    {
        var result = await _controller.Update(CategoryId, Category(CategoryId + 1));

        StatusCodeOf(result).ShouldBe(400);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!);
    }

    [Fact]
    public async Task Update_Missing_Returns404()
    {
        _repository.RetrieveAsync(CategoryId).Returns((Category?)null);

        var result = await _controller.Update(CategoryId, Category(CategoryId));

        StatusCodeOf(result).ShouldBe(404);
    }

    [Fact]
    public async Task Update_Exists_Returns204AndUpdates()
    {
        var category = Category(CategoryId);
        _repository.RetrieveAsync(CategoryId).Returns(category);

        var result = await _controller.Update(CategoryId, category);

        StatusCodeOf(result).ShouldBe(204);
        await _repository.Received(1).UpdateAsync(CategoryId, category);
    }

    [Fact]
    public async Task Delete_Missing_Returns404()
    {
        _repository.RetrieveAsync(CategoryId).Returns((Category?)null);

        var result = await _controller.Delete(CategoryId);

        StatusCodeOf(result).ShouldBe(404);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 400)]
    [InlineData(null, 400)]
    public async Task Delete_Exists_MapsRepositoryResultToStatusCode(bool? deleted, int expected)
    {
        _repository.RetrieveAsync(CategoryId).Returns(Category(CategoryId));
        _repository.DeleteAsync(CategoryId).Returns(deleted);

        var result = await _controller.Delete(CategoryId);

        StatusCodeOf(result).ShouldBe(expected);
    }

    private static Category Category(int id, string? name = null) =>
        new() { CategoryId = id, CategoryName = name ?? $"Category {id}" };

    private static int? StatusCodeOf(IActionResult? result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;

    private static T ValueOf<T>(IActionResult? result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeAssignableTo<T>()!;
}
