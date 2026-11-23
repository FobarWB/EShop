using System.Globalization;
using System.Text;
using System.Text.Json;
using EShop.OrderApi.Contracts;

namespace EShop.NotificationService.Email;

public sealed record OrderEmail(string To, string Subject, string Body);

// Turns an order event into the email for the buyer.
public static class OrderEmails
{
    // false: not an order event (a poison message, retrying cannot help). Otherwise email is null when the buyer
    // has no address: nothing to send. Throws JsonException for a body that does not match the contract.
    public static bool TryCompose(string? type, ReadOnlyMemory<byte> body, out OrderEmail? email)
    {
        switch (type)
        {
            case OrderEventTypes.OrderConfirmed:
                var confirmed = Deserialize<OrderConfirmed>(body);
                email = confirmed.Email is null ? null : Confirmed(confirmed, confirmed.Email);
                return true;
            case OrderEventTypes.OrderRejected:
                var rejected = Deserialize<OrderRejected>(body);
                email = rejected.Email is null ? null : Rejected(rejected, rejected.Email);
                return true;
            case OrderEventTypes.OrderCancelled:
                var cancelled = Deserialize<OrderCancelled>(body);
                email = cancelled.Email is null ? null : Cancelled(cancelled, cancelled.Email);
                return true;
            default:
                email = null;
                return false;
        }
    }

    // The stock is reserved (#76): the order is accepted.
    private static OrderEmail Confirmed(OrderConfirmed order, string to)
    {
        var body = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Hello {order.ShipName},")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"thank you for your order #{order.OrderId}.")
            .AppendLine();
        foreach (var item in order.Items)
        {
            body.AppendLine(
                CultureInfo.InvariantCulture,
                $"  {item.ProductName} x {item.Quantity}   {item.UnitPrice * item.Quantity:0.00}"
            );
        }
        body.AppendLine()
            .AppendLine(
                CultureInfo.InvariantCulture,
                $"Total (shipping included): {order.Total:0.00}"
            )
            .AppendLine();
        if (order.PlacedAsGuest)
        {
            // Keycloak sends the link itself (CustomerApi asks for it): this email only says which one to open.
            body.AppendLine(CultureInfo.InvariantCulture, $"We created an account for {to}.")
                .AppendLine(
                    "To see this order in your account, set your password with the link in our"
                )
                .AppendLine("separate email \"Update Your Account\".")
                .AppendLine();
        }
        body.Append("EShop");

        return new OrderEmail(to, $"Order #{order.OrderId} accepted", body.ToString());
    }

    // ProductApi could not reserve every line: nothing was taken, the buyer may order again with what is available.
    private static OrderEmail Rejected(OrderRejected order, string to)
    {
        var body = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Hello {order.ShipName},")
            .AppendLine()
            .AppendLine(
                CultureInfo.InvariantCulture,
                $"unfortunately we could not accept your order #{order.OrderId}:"
            )
            .AppendLine("some products are not available in the quantity you ordered.")
            .AppendLine();
        foreach (var item in order.Items)
        {
            body.AppendLine(
                CultureInfo.InvariantCulture,
                $"  {item.ProductName}: ordered {item.Quantity}, available {item.Available}"
            );
        }
        body.AppendLine()
            .AppendLine("Nothing was reserved for this order. You are welcome to order again")
            .AppendLine("with the quantities that are available.")
            .AppendLine()
            .Append("EShop");

        return new OrderEmail(to, $"Order #{order.OrderId} could not be accepted", body.ToString());
    }

    private static OrderEmail Cancelled(OrderCancelled order, string to)
    {
        var body = new StringBuilder()
            .AppendLine("Hello,")
            .AppendLine()
            .AppendLine(
                CultureInfo.InvariantCulture,
                $"your order #{order.OrderId} was cancelled on {order.CancelledAt:yyyy-MM-dd HH:mm} UTC."
            )
            .AppendLine()
            .Append("EShop");

        return new OrderEmail(to, $"Order #{order.OrderId} cancelled", body.ToString());
    }

    // Web defaults: camelCase, as the producer serialized it.
    private static T Deserialize<T>(ReadOnlyMemory<byte> body)
        where T : class =>
        JsonSerializer.Deserialize<T>(body.Span, JsonSerializerOptions.Web)
        ?? throw new JsonException("The message body is null.");
}
