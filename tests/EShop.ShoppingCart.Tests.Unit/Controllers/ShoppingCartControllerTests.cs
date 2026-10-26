using System.Security.Claims;
using EShop.ShoppingCart.Controllers;
using EShop.ShoppingCart.Dtos;
using EShop.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Http;
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
        )
        {
            // The signed-in user: the controller takes the user id from the token's sub claim.
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity([new Claim("sub", UserId)], "Test")
                    ),
                },
            },
        };
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
        _repository
            .AddItemToCartAsync(UserId, ProductId, 1, Arg.Any<CancellationToken>())
            .Returns(repositoryResult);

        var result = await _controller.AddItemToCart(Request(), CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Fact]
    public async Task AddItemToCart_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();

        await _controller.AddItemToCart(Request(), cts.Token);

        await _repository.Received(1).AddItemToCartAsync(UserId, ProductId, 1, cts.Token);
    }

    [Fact]
    public async Task GetCart_CartMissing_Returns404()
    {
        _repository
            .RetrieveCartAsync(UserId, Arg.Any<CancellationToken>())
            .Returns((CartResponse?)null);

        var result = await _controller.GetCart(CancellationToken.None);

        StatusCodeOf(result.Result.ShouldNotBeNull()).ShouldBe(404);
    }

    [Fact]
    public async Task GetCart_CartExists_Returns200WithCart()
    {
        var cart = new CartResponse(5, [new CartItemResponse(ProductId, 2)]);
        _repository.RetrieveCartAsync(UserId, Arg.Any<CancellationToken>()).Returns(cart);

        var result = await _controller.GetCart(CancellationToken.None);

        result.Value.ShouldBe(cart);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 400)]
    public async Task UpdateItemQuantity_MapsRepositoryResultToStatusCode(
        bool updated,
        int expected
    )
    {
        _repository
            .UpdateItemQuantityAsync(UserId, ProductId, 3, Arg.Any<CancellationToken>())
            .Returns(updated);

        var result = await _controller.UpdateItemQuantity(
            ProductId,
            new UpdateCartItemRequest { Quantity = 3 },
            CancellationToken.None
        );

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
        _repository
            .RemoveItemFromCartAsync(UserId, ProductId, Arg.Any<CancellationToken>())
            .Returns(removed);

        var result = await _controller.RemoveItemFromCart(ProductId, CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 400)]
    public async Task ClearCart_MapsRepositoryResultToStatusCode(bool cleared, int expected)
    {
        _repository.ClearCartAsync(UserId, Arg.Any<CancellationToken>()).Returns(cleared);

        var result = await _controller.ClearCart(CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, 404)]
    [InlineData(false, 400)]
    [InlineData(true, 200)]
    public async Task DeleteCart_MapsRepositoryResultToStatusCode(bool? deleted, int expected)
    {
        _repository.DeleteCartAsync(UserId, Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await _controller.DeleteCart(CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    private static AddCartItemRequest Request() => new() { ProductId = ProductId, Quantity = 1 };

    private static int? StatusCodeOf(IActionResult result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;
}
