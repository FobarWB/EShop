using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.OrderApi.Contracts;
using EShop.ProductApi.Contracts;
using EShop.ProductApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EShop.ProductApi.Reservations;

// The catalog's part of the checkout saga (#76). order.placed reserves the stock of all lines or of none and answers
// with stock.reserved or stock.rejected; order.cancelled returns what was reserved, once. A stock_reservations row
// records the decision per order, so a redelivered or reordered event changes nothing.
// Every stock change goes through ProductEvents.AddChanged: a new revision and a ProductChanged for the replicas.
// Runs inside the consumer's transaction, so the locks below hold until the stock, the events, the answer and the
// inbox row are committed together.
public sealed class OrderEventsHandler : IMessageHandler
{
    private readonly EShopDbContext _db;
    private readonly ILogger<OrderEventsHandler> _logger;

    public OrderEventsHandler(EShopDbContext db, ILogger<OrderEventsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<HandleResult> HandleAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken
    ) =>
        message.Type switch
        {
            OrderEventTypes.OrderPlaced => ReserveAsync(
                message.Deserialize<OrderPlaced>(),
                cancellationToken
            ),
            OrderEventTypes.OrderCancelled => ReleaseAsync(
                message.Deserialize<OrderCancelled>(),
                cancellationToken
            ),
            _ => Task.FromResult(HandleResult.Reject($"its type '{message.Type}' is unknown")),
        };

    private async Task<HandleResult> ReserveAsync(
        OrderPlaced placed,
        CancellationToken cancellationToken
    )
    {
        // Decided already: a redelivery, or the tombstone of an order cancelled before this event arrived.
        // Two first deliveries at the same time both pass here; the primary key lets one INSERT through, the other
        // fails and is retried, and then stops here.
        if (
            await _db.StockReservations.AnyAsync(
                r => r.OrderId == placed.OrderId,
                cancellationToken
            )
        )
        {
            _logger.LogInformation("Order {OrderId}: stock decided already", placed.OrderId);
            return HandleResult.Processed;
        }

        // One line per product: two lines of one product must fit the stock together.
        var lines = placed
            .Items.GroupBy(i => i.ProductId)
            .Select(g => new StockReservationLine
            {
                ProductId = g.Key,
                Quantity = g.Sum(i => i.Quantity),
            })
            .ToList();
        var products = await LockProductsAsync(lines.Select(l => l.ProductId), cancellationToken);
        // Every short line, not only the first: the buyer can fix the whole cart at once.
        var shortages = lines
            .Select(l => ShortageOf(l, products.GetValueOrDefault(l.ProductId)))
            .OfType<StockShortage>()
            .ToList();
        var now = DateTime.UtcNow;

        if (shortages.Count == 0)
        {
            foreach (var line in lines)
            {
                var product = products[line.ProductId];
                product.UnitsInStock = (short)(product.UnitsInStock - line.Quantity);
                ProductEvents.AddChanged(_db, product, now);
            }
        }
        _db.StockReservations.Add(
            new StockReservation
            {
                OrderId = placed.OrderId,
                Status =
                    shortages.Count == 0
                        ? StockReservationStatus.Reserved
                        : StockReservationStatus.Rejected,
                CreatedAt = now,
                Lines = lines,
            }
        );
        _db.OutboxMessages.Add(
            shortages.Count == 0
                ? OutboxMessageFactory.Create(
                    ProductApiOutbox.Producer,
                    ProductEventTypes.StockReserved,
                    new StockReserved(placed.OrderId, now),
                    now
                )
                : OutboxMessageFactory.Create(
                    ProductApiOutbox.Producer,
                    ProductEventTypes.StockRejected,
                    new StockRejected(placed.OrderId, shortages, now),
                    now
                )
        );
        _logger.LogInformation(
            "Order {OrderId}: stock {Result}, {Shortages} lines short",
            placed.OrderId,
            shortages.Count == 0 ? "reserved" : "rejected",
            shortages.Count
        );
        return HandleResult.Processed;
    }

    private async Task<HandleResult> ReleaseAsync(
        OrderCancelled cancelled,
        CancellationToken cancellationToken
    )
    {
        // Locked: two deliveries at the same time would both read Reserved and both return the stock.
        var reservation = (
            await _db
                .StockReservations.FromSql(
                    $"SELECT * FROM stock_reservations WHERE order_id = {cancelled.OrderId} FOR UPDATE"
                )
                .ToListAsync(cancellationToken)
        ).SingleOrDefault();
        var now = DateTime.UtcNow;

        if (reservation == null)
        {
            // Its order.placed is still to come (on a retry): the tombstone makes it take nothing.
            _db.StockReservations.Add(
                new StockReservation
                {
                    OrderId = cancelled.OrderId,
                    Status = StockReservationStatus.Cancelled,
                    CreatedAt = now,
                }
            );
            _logger.LogInformation(
                "Order {OrderId} cancelled before its stock was reserved",
                cancelled.OrderId
            );
            return HandleResult.Processed;
        }
        if (reservation.Status != StockReservationStatus.Reserved)
        {
            // Rejected: nothing was taken. Released or Cancelled: a redelivery.
            _logger.LogInformation(
                "Order {OrderId} cancelled; its stock is {Status}, nothing to return",
                cancelled.OrderId,
                reservation.Status
            );
            return HandleResult.Processed;
        }

        await _db.Entry(reservation).Collection(r => r.Lines).LoadAsync(cancellationToken);
        var products = await LockProductsAsync(
            reservation.Lines.Select(l => l.ProductId),
            cancellationToken
        );
        foreach (var line in reservation.Lines)
        {
            // A product deleted since the reservation has no stock to return to.
            if (products.TryGetValue(line.ProductId, out var product))
            {
                product.UnitsInStock = (short)(product.UnitsInStock + line.Quantity);
                ProductEvents.AddChanged(_db, product, now);
            }
        }
        reservation.Status = StockReservationStatus.Released;
        reservation.ReleasedAt = now;
        _logger.LogInformation("Order {OrderId} cancelled; stock returned", cancelled.OrderId);
        return HandleResult.Processed;
    }

    // FOR UPDATE: the rows stay locked until the consumer's transaction commits, so a second order for the same product
    // waits here and then reads the stock this one left; without the lock both would read the same stock and the
    // second UPDATE would fail on xmin and go to a retry. ORDER BY: every order locks its products in the same order,
    // so two orders sharing products never wait for each other (a deadlock).
    // xmin by name: SELECT * leaves out system columns, and Product.Version is mapped to it.
    private async Task<Dictionary<int, Product>> LockProductsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken
    )
    {
        var ids = productIds.ToArray();
        var products = await _db
            .Products.FromSql(
                $"SELECT p.*, p.xmin FROM products p WHERE p.product_id = ANY({ids}) ORDER BY p.product_id FOR UPDATE"
            )
            .ToListAsync(cancellationToken);
        return products.ToDictionary(p => p.ProductId);
    }

    // null: the line can be reserved. A product that is unknown (deleted, or the ProductId 0 of an order.placed older
    // than #76) or not for sale has nothing available. A quantity below 1 is never reserved: OrderApi validates it,
    // the catalog does not rely on that.
    private static StockShortage? ShortageOf(StockReservationLine line, Product? product)
    {
        var available =
            product == null || product.Discontinued || product.UnitPrice == null
                ? 0
                : product.UnitsInStock;
        return line.Quantity > 0 && available >= line.Quantity
            ? null
            : new StockShortage(line.ProductId, line.Quantity, available);
    }
}
