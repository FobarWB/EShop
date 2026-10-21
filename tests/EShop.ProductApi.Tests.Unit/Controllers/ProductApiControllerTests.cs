using EShop.Data.Entities;
using EShop.ProductApi.Controllers;
using EShop.ProductApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EShop.ProductApi.Tests.Unit.Controllers;

public class ProductApiControllerTests
{
    private const int ProductId = 7;

    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly ProductApiController _controller;

    public ProductApiControllerTests()
    {
        _controller = new ProductApiController(_repository);
    }

    [Fact]
    public async Task GetProducts_NoCategory_ReturnsAllProducts()
    {
        _repository
            .RetrieveAllAsync()
            .Returns([Product(1, categoryId: 1), Product(2, categoryId: 2)]);

        var result = await _controller.GetProducts(categoryId: null);

        ValueOf<IEnumerable<Product>>(result.Result).Count().ShouldBe(2);
    }

    [Fact]
    public async Task GetProducts_WithCategory_ReturnsOnlyThatCategory()
    {
        _repository
            .RetrieveAllAsync()
            .Returns([Product(1, categoryId: 1), Product(2, categoryId: 2)]);

        var result = await _controller.GetProducts(categoryId: 2);

        ValueOf<IEnumerable<Product>>(result.Result).ShouldHaveSingleItem().ProductId.ShouldBe(2);
    }

    [Fact]
    public async Task GetProduct_Missing_Returns404()
    {
        _repository.RetrieveAsync(ProductId).Returns((Product?)null);

        var result = await _controller.GetProduct(ProductId);

        StatusCodeOf(result).ShouldBe(404);
    }

    [Fact]
    public async Task GetProduct_Exists_Returns200WithProduct()
    {
        var product = Product(ProductId);
        _repository.RetrieveAsync(ProductId).Returns(product);

        var result = await _controller.GetProduct(ProductId);

        ValueOf<Product>(result).ShouldBe(product);
    }

    [Fact]
    public async Task Create_Created_Returns201WithRouteToNewProduct()
    {
        var product = Product(ProductId);
        _repository.CreateAsync(product).Returns(product);

        var result = await _controller.Create(product);

        var created = result.ShouldBeOfType<CreatedAtRouteResult>();
        created.RouteName.ShouldBe(nameof(ProductApiController.GetProduct));
        created.RouteValues!["id"].ShouldBe(ProductId);
    }

    [Fact]
    public async Task Create_RepositoryFailed_Returns400()
    {
        _repository.CreateAsync(Arg.Any<Product>()).Returns((Product?)null);

        var result = await _controller.Create(Product(ProductId));

        StatusCodeOf(result).ShouldBe(400);
    }

    [Fact]
    public async Task Update_IdMismatch_Returns400WithoutUpdating()
    {
        var result = await _controller.Update(ProductId, Product(ProductId + 1));

        StatusCodeOf(result).ShouldBe(400);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default, default!);
    }

    [Fact]
    public async Task Update_Missing_Returns404()
    {
        _repository.RetrieveAsync(ProductId).Returns((Product?)null);

        var result = await _controller.Update(ProductId, Product(ProductId));

        StatusCodeOf(result).ShouldBe(404);
    }

    [Fact]
    public async Task Update_Exists_Returns204AndUpdates()
    {
        var product = Product(ProductId);
        _repository.RetrieveAsync(ProductId).Returns(product);

        var result = await _controller.Update(ProductId, product);

        StatusCodeOf(result).ShouldBe(204);
        await _repository.Received(1).UpdateAsync(ProductId, product);
    }

    [Theory]
    [InlineData(DeleteResult.Deleted, 204)]
    [InlineData(DeleteResult.NotFound, 404)]
    [InlineData(DeleteResult.InUse, 409)]
    public async Task Delete_MapsRepositoryResultToStatusCode(DeleteResult deleted, int expected)
    {
        _repository.DeleteAsync(ProductId, Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await _controller.Delete(ProductId, TestContext.Current.CancellationToken);

        StatusCodeOf(result).ShouldBe(expected);
    }

    private static Product Product(int id, int? categoryId = null) =>
        new()
        {
            ProductId = id,
            ProductName = $"Product {id}",
            CategoryId = categoryId,
        };

    private static int? StatusCodeOf(IActionResult? result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;

    private static T ValueOf<T>(IActionResult? result) =>
        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeAssignableTo<T>()!;
}
