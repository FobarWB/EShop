using System.Net;
using System.Net.Http.Json;
using EShop.OrderApi.Controllers;

namespace EShop.OrderApi.Tests.Integration.Api;

// Its own API instance with a limit of 2 per minute, so the other guest tests never share this window.
public class GuestCheckoutRateLimitTests : IClassFixture<RateLimitedOrderApiFactory>
{
    private readonly RateLimitedOrderApiFactory _factory;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public GuestCheckoutRateLimitTests(RateLimitedOrderApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PlaceGuestOrder_AboveLimit_Returns429()
    {
        using var guest = _factory.CreateClient();

        // Invalid bodies count too: the limiter runs before validation, so nothing reaches the database.
        var first = await PostAsync(guest);
        var second = await PostAsync(guest);
        var third = await PostAsync(guest);

        first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private Task<HttpResponseMessage> PostAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/orders/guest")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Add(OrdersController.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        return client.SendAsync(request, _cancellationToken);
    }
}
