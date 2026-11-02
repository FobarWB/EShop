using System.Text;
using System.Text.Json;
using EShop.NotificationService.Email;
using EShop.OrderApi.Contracts;

namespace EShop.NotificationService.Tests.Unit.Email;

public class OrderEmailsTests
{
    private const string Email = "jane@example.com";

    [Fact]
    public void TryCompose_OrderPlaced_ListsItemsAndTotal()
    {
        var body = Serialize(
            new OrderPlaced(
                11085,
                Email,
                "Jane Doe",
                41m,
                DateTime.UtcNow,
                [new OrderPlacedItem("Chai", 2, 18m)]
            )
        );

        var known = OrderEmails.TryCompose(OrderEventTypes.OrderPlaced, body, out var email);

        known.ShouldBeTrue();
        var composed = email.ShouldNotBeNull();
        (composed.To, composed.Subject).ShouldBe((Email, "Order #11085 placed"));
        composed.Body.ShouldContain("Hello Jane Doe,");
        composed.Body.ShouldContain("Chai x 2   36.00");
        composed.Body.ShouldContain("Total (shipping included): 41.00");
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
                OrderEventTypes.OrderPlaced,
                Encoding.UTF8.GetBytes("not json"),
                out _
            )
        );
    }

    private static byte[] Serialize<T>(T @event) =>
        JsonSerializer.SerializeToUtf8Bytes(@event, JsonSerializerOptions.Web);
}
