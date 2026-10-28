using System.Security.Claims;
using EShop.Data.Entities;
using EShop.OrderApi.Controllers;
using EShop.OrderApi.Dtos;
using EShop.OrderApi.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.OrderApi.Tests.Unit.Controllers;

public class OrdersControllerTests
{
    private const string UserId = "user-1";
    private const int ProductId = 42;

    private static readonly Guid Key = Guid.NewGuid();
    private static readonly PlaceOrderRequest Request = new()
    {
        ShipName = "Jane Doe",
        ShipAddress = "1 Main St",
        ShipCity = "Berlin",
        ShipPostalCode = "10115",
        ShipCountry = "Germany",
    };
    private static readonly OrderResponse Order = new(
        7,
        new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc),
        OrderStatus.Pending,
        "Jane Doe",
        "1 Main St",
        "Berlin",
        null,
        "10115",
        "Germany",
        5m,
        41m,
        [new OrderItemResponse(ProductId, "Chai", 18m, 2)]
    );

    private readonly IOrderRepository _repository = Substitute.For<IOrderRepository>();
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _controller = new OrdersController(_repository, NullLogger<OrdersController>.Instance)
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
    [InlineData(PlaceOrderStatus.Placed, 201)]
    [InlineData(PlaceOrderStatus.AlreadyPlaced, 201)]
    [InlineData(PlaceOrderStatus.CartEmpty, 400)]
    [InlineData(PlaceOrderStatus.ProductsUnavailable, 409)]
    [InlineData(PlaceOrderStatus.InsufficientStock, 409)]
    public async Task PlaceOrder_MapsRepositoryResultToStatusCode(
        PlaceOrderStatus status,
        int expected
    )
    {
        ReturnsResult(new PlaceOrderResult(status, Order, [ProductId]));

        var result = await _controller.PlaceOrder(Key, Request, CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Fact]
    public async Task PlaceOrder_Placed_PointsLocationToGetOrder()
    {
        ReturnsResult(new PlaceOrderResult(PlaceOrderStatus.Placed, Order));

        var result = await _controller.PlaceOrder(Key, Request, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        created.ActionName.ShouldBe(nameof(OrdersController.GetOrder));
        created.RouteValues.ShouldNotBeNull()["id"].ShouldBe(Order.OrderId);
        created.Value.ShouldBe(Order);
    }

    [Theory]
    [InlineData(PlaceOrderStatus.ProductsUnavailable)]
    [InlineData(PlaceOrderStatus.InsufficientStock)]
    public async Task PlaceOrder_ProductProblem_ListsProductIds(PlaceOrderStatus status)
    {
        ReturnsResult(new PlaceOrderResult(status, ProductIds: [ProductId]));

        var result = await _controller.PlaceOrder(Key, Request, CancellationToken.None);

        result
            .ShouldBeOfType<ObjectResult>()
            .Value.ShouldBeOfType<ProblemDetails>()
            .Extensions["productIds"]
            .ShouldBe(new[] { ProductId });
    }

    [Fact]
    public async Task PlaceOrder_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        ReturnsResult(new PlaceOrderResult(PlaceOrderStatus.Placed, Order));

        await _controller.PlaceOrder(Key, Request, cts.Token);

        await _repository.Received(1).PlaceOrderAsync(UserId, Key, Request, cts.Token);
    }

    [Fact]
    public async Task GetOrder_OrderMissing_Returns404()
    {
        _repository
            .RetrieveOrderAsync(UserId, 7, Arg.Any<CancellationToken>())
            .Returns((OrderResponse?)null);

        var result = await _controller.GetOrder(7, CancellationToken.None);

        StatusCodeOf(result.Result.ShouldNotBeNull()).ShouldBe(404);
    }

    [Fact]
    public async Task GetOrder_OrderExists_Returns200WithOrder()
    {
        _repository.RetrieveOrderAsync(UserId, 7, Arg.Any<CancellationToken>()).Returns(Order);

        var result = await _controller.GetOrder(7, CancellationToken.None);

        result.Value.ShouldBe(Order);
    }

    [Theory]
    [InlineData(ChangeOrderStatusResult.Changed, 200)]
    [InlineData(ChangeOrderStatusResult.NotFound, 404)]
    [InlineData(ChangeOrderStatusResult.NotAllowed, 409)]
    public async Task CancelOrder_MapsRepositoryResultToStatusCode(
        ChangeOrderStatusResult status,
        int expected
    )
    {
        ReturnsChangeResult(status);
        _repository.RetrieveOrderAsync(UserId, 7, Arg.Any<CancellationToken>()).Returns(Order);

        var result = await _controller.CancelOrder(7, CancellationToken.None);

        StatusCodeOf(result.Result.ShouldNotBeNull()).ShouldBe(expected);
    }

    [Fact]
    public async Task CancelOrder_CancelsOnlyOwnOrder()
    {
        ReturnsChangeResult(ChangeOrderStatusResult.Changed);

        await _controller.CancelOrder(7, CancellationToken.None);

        await _repository
            .Received(1)
            .ChangeStatusAsync(
                7,
                UserId,
                OrderStatus.Cancelled,
                UserId,
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(ChangeOrderStatusResult.Changed, 204)]
    [InlineData(ChangeOrderStatusResult.NotFound, 404)]
    [InlineData(ChangeOrderStatusResult.NotAllowed, 409)]
    public async Task ChangeStatus_MapsRepositoryResultToStatusCode(
        ChangeOrderStatusResult status,
        int expected
    )
    {
        ReturnsChangeResult(status);

        var result = await _controller.ChangeStatus(
            7,
            new ChangeOrderStatusRequest { Status = OrderStatus.Paid },
            CancellationToken.None
        );

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Fact]
    public async Task ChangeStatus_ChangesAnyOrderAndRecordsTheAdmin()
    {
        ReturnsChangeResult(ChangeOrderStatusResult.Changed);

        await _controller.ChangeStatus(
            7,
            new ChangeOrderStatusRequest { Status = OrderStatus.Paid },
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .ChangeStatusAsync(7, null, OrderStatus.Paid, UserId, Arg.Any<CancellationToken>());
    }

    private void ReturnsChangeResult(ChangeOrderStatusResult result) =>
        _repository
            .ChangeStatusAsync(
                7,
                Arg.Any<string?>(),
                Arg.Any<OrderStatus>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(result);

    private void ReturnsResult(PlaceOrderResult result) =>
        _repository
            .PlaceOrderAsync(UserId, Key, Request, Arg.Any<CancellationToken>())
            .Returns(result);

    private static int? StatusCodeOf(IActionResult result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;
}
