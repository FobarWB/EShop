using EShop.Messaging.Outbox;
using EShop.OrderApi.Contracts;
using EShop.OrderApi.Data;
using EShop.OrderApi.Data.Entities;
using EShop.OrderApi.Dtos;
using EShop.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EShop.OrderApi.Repositories;

public class OrderRepository : IOrderRepository
{
    // Flat shipping fee until delivery options exist; the cart page shows the same amount.
    public const decimal StandardFreight = 5m;

    private const string IdempotencyKeyIndex = "ix_orders_user_id_idempotency_key";
    private const string GuestIdempotencyKeyIndex = "ix_orders_guest_idempotency_key";

    // ChangedBy of a guest order's first journal row: there is no user yet.
    private const string GuestChangedBy = "guest";

    private readonly OrderingDbContext _db;

    public OrderRepository(OrderingDbContext db)
    {
        _db = db;
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(
        string userId,
        string? customerEmail,
        Guid idempotencyKey,
        PlaceOrderRequest request,
        CancellationToken cancellationToken
    )
    {
        var owner = new OrderOwner(userId, customerEmail, IsGuest: false);

        // A retry of an attempt that already succeeded (double click, lost response): the same order again.
        var existing = await RetrieveByKeyAsync(owner, idempotencyKey, cancellationToken);
        if (existing != null)
        {
            return new PlaceOrderResult(PlaceOrderStatus.AlreadyPlaced, existing);
        }
        return await CreateOrderAsync(
            owner,
            idempotencyKey,
            request.Address,
            request.Items,
            cancellationToken
        );
    }

    public Task<PlaceOrderResult> PlaceGuestOrderAsync(
        string email,
        Guid idempotencyKey,
        ShippingAddressRequest address,
        IReadOnlyList<OrderItemRequest> items,
        CancellationToken cancellationToken
    ) =>
        CreateOrderAsync(
            new OrderOwner(null, email, IsGuest: true),
            idempotencyKey,
            address,
            items,
            cancellationToken
        );

    public Task<OrderResponse?> RetrieveGuestOrderAsync(
        string email,
        Guid idempotencyKey,
        CancellationToken cancellationToken
    ) =>
        RetrieveByKeyAsync(
            new OrderOwner(null, email, IsGuest: true),
            idempotencyKey,
            cancellationToken
        );

    public Task<OrderResponse?> RetrieveOrderAsync(
        string userId,
        int orderId,
        CancellationToken cancellationToken
    ) =>
        _db
            .Orders.Where(o => o.UserId == userId && o.OrderId == orderId)
            .ToResponse()
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PagedResponse<OrderSummaryResponse>> RetrieveOrdersAsync(
        string userId,
        PageRequest paging,
        CancellationToken cancellationToken
    ) =>
        // ix_orders_user_id_order_date serves both the filter and the order; order_id breaks ties of equal dates,
        // so the paging stays stable.
        _db
            .Orders.Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.OrderId)
            .ToSummaryResponse()
            .ToPageAsync(paging, cancellationToken);

    public async Task<ChangeOrderStatusResult> ChangeStatusAsync(
        int orderId,
        string? ownerId,
        OrderStatus next,
        string changedBy,
        CancellationToken cancellationToken
    )
    {
        // Tracked: the status changes below. The xmin of the order is read here, and SaveChanges writes
        // the new status only if no other request changed the order since.
        var order = await _db.Orders.FirstOrDefaultAsync(
            o => o.OrderId == orderId && (ownerId == null || o.UserId == ownerId),
            cancellationToken
        );
        if (order == null)
        {
            return ChangeOrderStatusResult.NotFound;
        }
        // One instant for the status journal and the event.
        var changedAt = DateTime.UtcNow;
        if (!order.TryChangeStatus(next, changedBy, changedAt))
        {
            return ChangeOrderStatusResult.NotAllowed;
        }

        if (next == OrderStatus.Cancelled)
        {
            // ProductApi returns the stock of a reserved order when it receives this event (#76).
            _db.OutboxMessages.Add(
                OutboxMessageFactory.Create(
                    OrderApiOutbox.Producer,
                    OrderEventTypes.OrderCancelled,
                    new OrderCancelled(order.OrderId, order.CustomerEmail, changedAt),
                    changedAt
                )
            );
        }
        // One SaveChanges, one transaction: the status, its journal row and the event. A concurrent change of the
        // same order (a double cancel, the stock answer arriving) fails the xmin check: DbUpdateConcurrencyException, 409.
        await _db.SaveChangesAsync(cancellationToken);
        return ChangeOrderStatusResult.Changed;
    }

    // The checkout of both kinds of buyers: price the lines from the catalog's replica, store the Pending order and
    // its events. The stock is ProductApi's: it reserves it on order.placed and answers (#76).
    private async Task<PlaceOrderResult> CreateOrderAsync(
        OrderOwner owner,
        Guid idempotencyKey,
        ShippingAddressRequest address,
        IReadOnlyList<OrderItemRequest> items,
        CancellationToken cancellationToken
    )
    {
        // long: the lines come from the browser, and their sum must not overflow int.
        var quantities = items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
        var productIds = quantities.Keys.ToArray();
        var products = await _db
            .OrderProducts.AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId) && !p.Deleted)
            .ToDictionaryAsync(p => p.ProductId, cancellationToken);

        // Unknown or deleted (or its ProductChanged has not arrived yet), discontinued, or without a price:
        // the client marks these lines.
        var unavailable = productIds
            .Where(id =>
                !products.TryGetValue(id, out var product)
                || product.Discontinued
                || product.UnitPrice is null
            )
            .ToList();
        if (unavailable.Count > 0)
        {
            return new PlaceOrderResult(
                PlaceOrderStatus.ProductsUnavailable,
                ProductIds: unavailable
            );
        }

        // An early answer from the replica, which follows every reservation a moment later. ProductApi decides.
        var insufficient = productIds
            .Where(id => quantities[id] > products[id].UnitsInStock)
            .ToList();
        if (insufficient.Count > 0)
        {
            return new PlaceOrderResult(
                PlaceOrderStatus.InsufficientStock,
                ProductIds: insufficient
            );
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            UserId = owner.UserId,
            CustomerEmail = owner.Email,
            PlacedAsGuest = owner.IsGuest,
            IdempotencyKey = idempotencyKey,
            OrderDate = now,
            Freight = StandardFreight,
            ShipName = address.ShipName,
            ShipAddress = address.ShipAddress,
            ShipCity = address.ShipCity,
            ShipRegion = address.ShipRegion,
            ShipPostalCode = address.ShipPostalCode,
            ShipCountry = address.ShipCountry,
        };
        order.RecordPlaced(owner.UserId ?? GuestChangedBy, now);
        foreach (var (productId, quantity) in quantities)
        {
            var product = products[productId];
            order.OrderDetails.Add(
                new OrderDetail
                {
                    ProductId = productId,
                    // A copy of the current price (not null: checked above), so later price changes
                    // do not alter the order.
                    UnitPrice = product.UnitPrice!.Value,
                    ProductName = product.ProductName,
                    // Fits a smallint: at most units_in_stock (checked above).
                    Quantity = (short)quantity,
                }
            );
        }
        _db.Orders.Add(order);

        // Two SaveChanges, one transaction: the order (its id comes from the database) and then the events
        // that need this id are committed together or not at all.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: IdempotencyKeyIndex or GuestIdempotencyKeyIndex,
                    }
            )
        {
            // A concurrent request with the same key placed the order between the check and this save.
            // Inside a transaction SaveChanges works under a savepoint and rolls back to it on failure,
            // so the transaction is still usable for this read.
            var placed = await RetrieveByKeyAsync(owner, idempotencyKey, cancellationToken);
            return placed != null
                ? new PlaceOrderResult(PlaceOrderStatus.AlreadyPlaced, placed)
                // Only a guest's key can collide with another buyer's order: another email used it.
                : new PlaceOrderResult(PlaceOrderStatus.IdempotencyKeyReused);
        }

        // The order has its id now; the events are saved by the second SaveChanges, in the same transaction.
        _db.OutboxMessages.Add(
            OutboxMessageFactory.Create(
                OrderApiOutbox.Producer,
                OrderEventTypes.OrderPlaced,
                new OrderPlaced(
                    order.OrderId,
                    order.CustomerEmail,
                    order.ShipName,
                    order.OrderDetails.Sum(d => d.UnitPrice * d.Quantity) + StandardFreight,
                    now,
                    order
                        .OrderDetails.Select(d => new OrderPlacedItem(
                            d.ProductName,
                            d.Quantity,
                            d.UnitPrice,
                            d.ProductId
                        ))
                        .ToList(),
                    owner.IsGuest
                ),
                now
            )
        );
        if (owner is { IsGuest: true, Email: { } guestEmail })
        {
            // CustomerApi creates the account of this email; OrderApi then attaches the order to it.
            _db.OutboxMessages.Add(
                OutboxMessageFactory.Create(
                    OrderApiOutbox.Producer,
                    OrderEventTypes.GuestOrderPlaced,
                    new GuestOrderPlaced(order.OrderId, guestEmail, now),
                    now
                )
            );
        }
        await _db.SaveChangesAsync(cancellationToken);
        var created = await _db
            .Orders.Where(o => o.OrderId == order.OrderId)
            .ToResponse()
            .FirstOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PlaceOrderResult(PlaceOrderStatus.Placed, created);
    }

    private Task<OrderResponse?> RetrieveByKeyAsync(
        OrderOwner owner,
        Guid idempotencyKey,
        CancellationToken cancellationToken
    )
    {
        // The condition is chosen here, not in SQL: each query matches one of the two unique indexes.
        // A guest's key is checked together with the email, as the request has no user.
        var orders = owner.IsGuest
            ? _db.Orders.Where(o =>
                o.PlacedAsGuest
                && o.IdempotencyKey == idempotencyKey
                && o.CustomerEmail == owner.Email
            )
            : _db.Orders.Where(o => o.UserId == owner.UserId && o.IdempotencyKey == idempotencyKey);
        return orders.ToResponse().FirstOrDefaultAsync(cancellationToken);
    }

    // Who places the order: a signed-in user (UserId set) or a guest (only the email).
    private sealed record OrderOwner(string? UserId, string? Email, bool IsGuest);
}
