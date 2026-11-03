using System.Security.Claims;
using EShop.CustomerApi.Controllers;
using EShop.CustomerApi.Dtos;
using EShop.CustomerApi.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.CustomerApi.Tests.Unit.Controllers;

public class CustomersControllerTests
{
    private const string UserId = "user-1";
    private const int AddressId = 7;

    private static readonly AddressRequest Request = new()
    {
        RecipientName = "Jane Doe",
        Address = "1 Main St",
        City = "Berlin",
        PostalCode = "10115",
        Country = "Germany",
    };
    private static readonly AddressResponse Address = new(
        AddressId,
        "Jane Doe",
        "1 Main St",
        "Berlin",
        null,
        "10115",
        "Germany",
        true
    );

    private readonly ICustomerRepository _repository = Substitute.For<ICustomerRepository>();
    private readonly CustomersController _controller;

    public CustomersControllerTests()
    {
        _controller = new CustomersController(_repository, NullLogger<CustomersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            [
                                new Claim("sub", UserId),
                                new Claim("email", "jane@example.com"),
                                new Claim("given_name", "Jane"),
                                new Claim("family_name", "Doe"),
                            ],
                            "Test"
                        )
                    ),
                },
            },
        };
    }

    [Fact]
    public async Task GetProfile_TakesEmailAndNameFromTokenAndPhoneFromRepository()
    {
        _repository.RetrievePhoneAsync(UserId, Arg.Any<CancellationToken>()).Returns("+49 30 1");

        var result = await _controller.GetProfile(CancellationToken.None);

        result.Value.ShouldBe(
            new CustomerProfileResponse("jane@example.com", "Jane", "Doe", "+49 30 1")
        );
    }

    [Fact]
    public async Task GetAddress_AddressMissing_Returns404()
    {
        _repository
            .RetrieveAddressAsync(UserId, AddressId, Arg.Any<CancellationToken>())
            .Returns((AddressResponse?)null);

        var result = await _controller.GetAddress(AddressId, CancellationToken.None);

        StatusCodeOf(result.Result.ShouldNotBeNull()).ShouldBe(404);
    }

    [Theory]
    [InlineData(SaveAddressStatus.Saved, 201)]
    [InlineData(SaveAddressStatus.LimitReached, 409)]
    [InlineData(SaveAddressStatus.DefaultConflict, 409)]
    public async Task AddAddress_MapsRepositoryResultToStatusCode(
        SaveAddressStatus status,
        int expected
    )
    {
        _repository
            .AddAddressAsync(UserId, Request, Arg.Any<CancellationToken>())
            .Returns(new SaveAddressResult(status, Address));

        var result = await _controller.AddAddress(Request, CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Fact]
    public async Task AddAddress_Saved_PointsLocationToGetAddress()
    {
        _repository
            .AddAddressAsync(UserId, Request, Arg.Any<CancellationToken>())
            .Returns(new SaveAddressResult(SaveAddressStatus.Saved, Address));

        var result = await _controller.AddAddress(Request, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        created.ActionName.ShouldBe(nameof(CustomersController.GetAddress));
        created.RouteValues.ShouldNotBeNull()["id"].ShouldBe(AddressId);
    }

    [Theory]
    [InlineData(SaveAddressStatus.Saved, 200)]
    [InlineData(SaveAddressStatus.NotFound, 404)]
    [InlineData(SaveAddressStatus.DefaultConflict, 409)]
    public async Task UpdateAddress_MapsRepositoryResultToStatusCode(
        SaveAddressStatus status,
        int expected
    )
    {
        _repository
            .UpdateAddressAsync(UserId, AddressId, Request, Arg.Any<CancellationToken>())
            .Returns(new SaveAddressResult(status, Address));

        var result = await _controller.UpdateAddress(AddressId, Request, CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task DeleteAddress_MapsRepositoryResultToStatusCode(bool deleted, int expected)
    {
        _repository
            .DeleteAddressAsync(UserId, AddressId, Arg.Any<CancellationToken>())
            .Returns(deleted);

        var result = await _controller.DeleteAddress(AddressId, CancellationToken.None);

        StatusCodeOf(result).ShouldBe(expected);
    }

    private static int? StatusCodeOf(IActionResult result) =>
        result.ShouldBeAssignableTo<IStatusCodeActionResult>()!.StatusCode;
}
