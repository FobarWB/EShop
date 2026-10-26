using EShop.CategoryApi.Controllers;
using EShop.CategoryApi.Dtos;
using EShop.CategoryApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EShop.CategoryApi.Tests.Unit.Controllers;

public class CategoryApiControllerTests
{
    private const int CategoryId = 3;

    private readonly ICategoryRepository _repository = Substitute.For<ICategoryRepository>();
    private readonly CategoryAPIController _controller;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public CategoryApiControllerTests()
    {
        _controller = new CategoryAPIController(_repository);
    }

    // The repository filters in SQL; a blank name means no filter.
    [Theory]
    [InlineData("bev", "bev")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    public async Task GetCategories_PassesNameToRepository(
        string? categoryName,
        string? expectedName
    )
    {
        List<CategoryResponse> categories = [Response(1)];
        _repository
            .RetrieveAllAsync(expectedName, Arg.Any<CancellationToken>())
            .Returns(categories);

        var result = await _controller.GetCategories(categoryName, _cancellationToken);

        ValueOf<IEnumerable<CategoryResponse>>(result.Result).ShouldBe(categories);
    }

    [Fact]
    public async Task GetCategory_Missing_Returns404()
    {
        _repository
            .RetrieveAsync(CategoryId, Arg.Any<CancellationToken>())
            .Returns((CategoryResponse?)null);

        var result = await _controller.GetCategory(CategoryId, _cancellationToken);

        StatusCodeOf(result).ShouldBe(404);
    }

    [Fact]
    public async Task GetCategory_Exists_Returns200WithCategory()
    {
        var category = Response(CategoryId);
        _repository.RetrieveAsync(CategoryId, Arg.Any<CancellationToken>()).Returns(category);

        var result = await _controller.GetCategory(CategoryId, _cancellationToken);

        ValueOf<CategoryResponse>(result).ShouldBe(category);
    }

    [Fact]
    public async Task Create_Returns201WithRouteToNewCategory()
    {
        var request = Request();
        _repository
            .CreateAsync(request, Arg.Any<CancellationToken>())
            .Returns(Response(CategoryId));

        var result = await _controller.Create(request, _cancellationToken);

        var created = result.ShouldBeOfType<CreatedAtRouteResult>();
        created.RouteName.ShouldBe(nameof(CategoryAPIController.GetCategory));
        created.RouteValues!["id"].ShouldBe(CategoryId);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Update_MapsRepositoryResultToStatusCode(bool updated, int expected)
    {
        var request = Request();
        _repository.UpdateAsync(CategoryId, request, Arg.Any<CancellationToken>()).Returns(updated);

        var result = await _controller.Update(CategoryId, request, _cancellationToken);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(DeleteResult.Deleted, 204)]
    [InlineData(DeleteResult.NotFound, 404)]
    [InlineData(DeleteResult.InUse, 409)]
    public async Task Delete_MapsRepositoryResultToStatusCode(DeleteResult deleted, int expected)
    {
        _repository.DeleteAsync(CategoryId, Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await _controller.Delete(CategoryId, _cancellationToken);

        StatusCodeOf(result).ShouldBe(expected);
    }

    private static CategoryRequest Request() => new() { CategoryName = "Beverages" };

    private static CategoryResponse Response(int id) => new(id, $"Category {id}", null);

    private static int? StatusCodeOf(IActionResult? result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;

    private static T ValueOf<T>(IActionResult? result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeAssignableTo<T>()!;
}
