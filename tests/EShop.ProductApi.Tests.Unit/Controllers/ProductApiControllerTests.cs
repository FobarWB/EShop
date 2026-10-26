using EShop.ProductApi.Controllers;
using EShop.ProductApi.Dtos;
using EShop.ProductApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EShop.ProductApi.Tests.Unit.Controllers;

public class ProductApiControllerTests
{
    private const int ProductId = 7;

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly ProductApiController _controller;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public ProductApiControllerTests()
    {
        _controller = new ProductApiController(_repository);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public async Task GetProducts_PassesCategoryToRepository(int? categoryId)
    {
        // The repository filters in SQL; the controller returns what it gets.
        List<ProductResponse> products = [Response(1)];
        _repository.RetrieveAllAsync(categoryId, Arg.Any<CancellationToken>()).Returns(products);

        var result = await _controller.GetProducts(categoryId, _cancellationToken);

        ValueOf<IEnumerable<ProductResponse>>(result.Result).ShouldBe(products);
    }

    [Fact]
    public async Task GetProduct_Missing_Returns404()
    {
        _repository
            .RetrieveAsync(ProductId, Arg.Any<CancellationToken>())
            .Returns((ProductResponse?)null);

        var result = await _controller.GetProduct(ProductId, _cancellationToken);

        StatusCodeOf(result.Result).ShouldBe(404);
    }

    [Fact]
    public async Task GetProduct_Exists_Returns200WithProduct()
    {
        var product = Response(ProductId);
        _repository.RetrieveAsync(ProductId, Arg.Any<CancellationToken>()).Returns(product);

        var result = await _controller.GetProduct(ProductId, _cancellationToken);

        result.Value.ShouldBe(product);
    }

    [Fact]
    public async Task Create_Returns201WithRouteToNewProduct()
    {
        var request = Request();
        _repository.CreateAsync(request, Arg.Any<CancellationToken>()).Returns(Response(ProductId));

        var result = await _controller.Create(request, _cancellationToken);

        var created = result.Result.ShouldBeOfType<CreatedAtRouteResult>();
        created.RouteName.ShouldBe(nameof(ProductApiController.GetProduct));
        created.RouteValues!["id"].ShouldBe(ProductId);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Update_MapsRepositoryResultToStatusCode(bool updated, int expected)
    {
        var request = Request();
        _repository.UpdateAsync(ProductId, request, Arg.Any<CancellationToken>()).Returns(updated);

        var result = await _controller.Update(ProductId, request, _cancellationToken);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(DeleteResult.Deleted, 204)]
    [InlineData(DeleteResult.NotFound, 404)]
    [InlineData(DeleteResult.InUse, 409)]
    public async Task Delete_MapsRepositoryResultToStatusCode(DeleteResult deleted, int expected)
    {
        _repository.DeleteAsync(ProductId, Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await _controller.Delete(ProductId, _cancellationToken);

        StatusCodeOf(result).ShouldBe(expected);
    }

    private static ProductRequest Request() => new() { ProductName = "Chai" };

    private static ProductResponse Response(int id) =>
        new(id, $"Product {id}", null, null, null, null, 0, null, null, false);

    private static int? StatusCodeOf(IActionResult? result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;

    private static T ValueOf<T>(IActionResult? result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeAssignableTo<T>()!;
}
