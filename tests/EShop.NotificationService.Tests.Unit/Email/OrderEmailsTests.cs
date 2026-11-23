using System.Text;
using System.Text.Json;
using EShop.NotificationService.Email;
using EShop.OrderApi.Contracts;

namespace EShop.NotificationService.Tests.Unit.Email;

public class OrderEmailsTests
{
    private const string Email = "jane@example.com";

    [Fact]
    public void TryCompose_OrderConfirmed_ListsItemsAndTotal()
    {
        var body = Serialize(Confirmed(placedAsGuest: false));

        var known = OrderEmails.TryCompose(OrderEventTypes.OrderConfirmed, body, out var email);

        known.ShouldBeTrue();
        var composed = email.ShouldNotBeNull();
        (composed.To, composed.Subject).ShouldBe((Email, "Order #11085 accepted"));
        composed.Body.ShouldContain("Hello Jane Doe,");
        composed.Body.ShouldContain("Chai x 2   36.00");
        composed.Body.ShouldContain("Total (shipping included): 41.00");
    }

    [Fact]
    public void TryCompose_GuestOrderConfirmed_PointsToTheSetPasswordEmail()
    {
        var body = Serialize(Confirmed(placedAsGuest: true));

        OrderEmails.TryCompose(OrderEventTypes.OrderConfirmed, body, out var email);

        var composed = email.ShouldNotBeNull();
        composed.Body.ShouldContain($"We created an account for {Email}.");
        composed.Body.ShouldContain("\"Update Your Account\"");
    }

    [Fact]
    public void TryCompose_SignedInOrderConfirmed_SaysNothingAboutAnAccount()
    {
        var body = Serialize(Confirmed(placedAsGuest: false));

        OrderEmails.TryCompose(OrderEventTypes.OrderConfirmed, body, out var email);

        email.ShouldNotBeNull().Body.ShouldNotContain("account");
    }

    [Fact]
    public void TryCompose_OrderRejected_ListsTheShortLines()
    {
        var body = Serialize(
            new OrderRejected(
                11085,
                Email,
                "Jane Doe",
                DateTime.UtcNow,
                [new OrderRejectedItem("Chai", 2, 1), new OrderRejectedItem("Tofu", 1, 0)]
            )
        );

        var known = OrderEmails.TryCompose(OrderEventTypes.OrderRejected, body, out var email);

        known.ShouldBeTrue();
        var composed = email.ShouldNotBeNull();
        (composed.To, composed.Subject).ShouldBe((Email, "Order #11085 could not be accepted"));
        composed.Body.ShouldContain("Chai: ordered 2, available 1");
        composed.Body.ShouldContain("Tofu: ordered 1, available 0");
    }

    [Fact]
    public void TryCompose_OrderPlaced_IsNoLongerAnEmail()
    {
        // Retired in #76: the buyer hears of the order once it is accepted or rejected.
        var known = OrderEmails.TryCompose(
            OrderEventTypes.OrderPlaced,
            Encoding.UTF8.GetBytes("{}"),
            out var email
        );

        known.ShouldBeFalse();
        email.ShouldBeNull();
    }

    [Fact]
    public void TryCompose_OrderCancelled_NamesOrderAndTime()
    {
        var body = Serialize(
            new OrderCancelled(11085, Email, new DateTime(2026, 11, 2, 11, 59, 0, DateTimeKind.Utc))
        );

        var known = OrderEmails.TryCompose(OrderEventTypes.OrderCancelled, body, out var email);

        known.ShouldBeTrue();
        var composed = email.ShouldNotBeNull();
        composed.Subject.ShouldBe("Order #11085 cancelled");
        composed.Body.ShouldContain("cancelled on 2026-11-02 11:59 UTC");
    }

    [Fact]
    public void TryCompose_NoEmailAddress_KnownWithoutEmail()
    {
        var body = Serialize(new OrderCancelled(1, null, DateTime.UtcNow));

        var known = OrderEmails.TryCompose(OrderEventTypes.OrderCancelled, body, out var email);

        known.ShouldBeTrue();
        email.ShouldBeNull();
    }

    [Fact]
    public void TryCompose_UnknownType_ReturnsFalse()
    {
        var known = OrderEmails.TryCompose(
            "order.shipped",
            Encoding.UTF8.GetBytes("{}"),
            out var email
        );

        known.ShouldBeFalse();
        email.ShouldBeNull();
    }

    [Fact]
    public void TryCompose_BodyIsNotJson_Throws()
    {
        Should.Throw<JsonException>(() =>
            OrderEmails.TryCompose(
                OrderEventTypes.OrderConfirmed,
                Encoding.UTF8.GetBytes("not json"),
                out _
            )
        );
    }

    private static OrderConfirmed Confirmed(bool placedAsGuest) =>
        new(
            11085,
            "user-1",
            Email,
            "Jane Doe",
            41m,
            placedAsGuest,
            DateTime.UtcNow,
            [new OrderConfirmedItem(1, "Chai", 2, 18m)]
        );

    private static byte[] Serialize<T>(T @event) =>
        JsonSerializer.SerializeToUtf8Bytes(@event, JsonSerializerOptions.Web);
}
