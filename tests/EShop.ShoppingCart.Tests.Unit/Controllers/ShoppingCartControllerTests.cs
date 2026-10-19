using EShop.Data.Entities;
using EShop.ShoppingCart.Controllers;
using EShop.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.ShoppingCart.Tests.Unit.Controllers;

public class ShoppingCartControllerTests
{
    private const string UserId = "user-1";
    private const int ProductId = 42;

    private readonly IShoppingCartRepository _repository =
        Substitute.For<IShoppingCartRepository>();
    private readonly ShoppingCartController _controller;

    public ShoppingCartControllerTests()
    {
        _controller = new ShoppingCartController(
            _repository,
            NullLogger<ShoppingCartController>.Instance
        );
    }

    [Fact]
    public async Task CreateCart_EmptyUserId_Returns400()
    {
        var result = await _controller.CreateCart(new Cart { UserId = "" });

        StatusCodeOf(result).ShouldBe(400);
        await _repository.DidNotReceiveWithAnyArgs().CreateCartAsync(default!);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 400)]
    public async Task CreateCart_MapsRepositoryResultToStatusCode(bool created, int expected)
    {
        _repository
            .CreateCartAsync(Arg.Any<Cart>())
            .Returns(call => created ? call.Arg<Cart>() : null);

        var result = await _controller.CreateCart(new Cart { UserId = UserId });

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(AddItemResult.Added, 200)]
    [InlineData(AddItemResult.ProductNotFound, 404)]
    [InlineData(AddItemResult.InsufficientStock, 409)]
    public async Task AddItemToCart_MapsRepositoryResultToStatusCode(
        AddItemResult repositoryResult,
        int expected
    )
    {
        var item = new CartItem { ProductId = ProductId, Quantity = 1 };
        _repository
            .AddItemToCartAsync(UserId, item, Arg.Any<CancellationToken>())
            .Returns(repositoryResult);

        var result = await _controller.AddItemToCart(UserId, item, CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(ProductId, 0)]
    [InlineData(ProductId, -1)]
    public async Task AddItemToCart_InvalidItem_Returns400WithoutCallingRepository(
        int productId,
        int quantity
    )
    {
        var item = new CartItem { ProductId = productId, Quantity = quantity };

        var result = await _controller.AddItemToCart(UserId, item, CancellationToken.None);

        StatusCodeOf(result).ShouldBe(400);
        await _repository
            .DidNotReceiveWithAnyArgs()
            .AddItemToCartAsync(
                Arg.Any<string>(),
                Arg.Any<CartItem>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task AddItemToCart_PassesCancellationTokenToRepository()
    {
        var item = new CartItem { ProductId = ProductId, Quantity = 1 };
        using var cts = new CancellationTokenSource();

        await _controller.AddItemToCart(UserId, item, cts.Token);

        await _repository.Received(1).AddItemToCartAsync(UserId, item, cts.Token);
    }

    [Fact]
    public async Task GetCartByUserId_CartMissing_Returns404()
    {
        _repository.RetrieveCartByUserIdAsync(UserId).Returns((Cart?)null);

        var result = await _controller.GetCartByUserId(UserId);

        StatusCodeOf(result).ShouldBe(404);
    }

    [Fact]
    public async Task GetCartByUserId_CartExists_Returns200WithCart()
    {
        var cart = new Cart { UserId = UserId };
        _repository.RetrieveCartByUserIdAsync(UserId).Returns(cart);

        var result = await _controller.GetCartByUserId(UserId);

        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(cart);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 400)]
    public async Task UpdateItemQuantity_MapsRepositoryResultToStatusCode(
        bool updated,
        int expected
    )
    {
        _repository.UpdateItemQuantityAsync(UserId, ProductId, 3).Returns(updated);

        var result = await _controller.UpdateItemQuantity(UserId, ProductId, 3);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 400)]
    public async Task RemoveItemFromCart_MapsRepositoryResultToStatusCode(
        bool removed,
        int expected
    )
    {
        _repository.RemoveItemFromCartAsync(UserId, ProductId).Returns(removed);

        var result = await _controller.RemoveItemFromCart(UserId, ProductId);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 400)]
    public async Task ClearCart_MapsRepositoryResultToStatusCode(bool cleared, int expected)
    {
        _repository.ClearCartAsync(UserId).Returns(cleared);

        var result = await _controller.ClearCart(UserId);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, 404)]
    [InlineData(false, 400)]
    [InlineData(true, 200)]
    public async Task DeleteCart_MapsRepositoryResultToStatusCode(bool? deleted, int expected)
    {
        _repository.DeleteCartAsync(UserId).Returns(deleted);

        var result = await _controller.DeleteCart(UserId);

        StatusCodeOf(result).ShouldBe(expected);
    }

    private static int? StatusCodeOf(IActionResult result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;
}
