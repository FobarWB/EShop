using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.Outbox;
using EShop.OrderApi.Contracts;
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

    private readonly EShopDbContext _db;

    public OrderRepository(EShopDbContext db)
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

        // Tracked: the stock and the cart change below. The xmin of each product is read here,
        // and SaveChanges writes the new stock only if the row has not changed since.
        var cart = await _db
            .Carts.Include(c => c.CartItems)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (cart == null || cart.CartItems.Count == 0)
        {
            return new PlaceOrderResult(PlaceOrderStatus.CartEmpty);
        }

        // cart_items.product_id is a required foreign key: Include always loads the product.
        var lines = cart.CartItems.Select(i => new OrderLine(i.Product!, i.Quantity)).ToList();
        return await CreateOrderAsync(
            owner,
            idempotencyKey,
            request,
            lines,
            () => cart.CartItems.Clear(),
            cancellationToken
        );
    }

    public async Task<PlaceOrderResult> PlaceGuestOrderAsync(
        string email,
        Guid idempotencyKey,
        PlaceOrderRequest address,
        IReadOnlyList<GuestOrderItem> items,
        CancellationToken cancellationToken
    )
    {
        // long: the lines come from the browser, and their sum must not overflow int.
        var quantities = items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
        var productIds = quantities.Keys.ToArray();

        // Tracked, as the cart's products: the stock changes, and SaveChanges checks each product's xmin.
        var products = await _db
            .Products.Where(p => productIds.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        // A product deleted since the browser stored it: the client marks it, as a discontinued one.
        var missing = productIds.Except(products.Select(p => p.ProductId)).ToList();
        if (missing.Count > 0)
        {
            return new PlaceOrderResult(PlaceOrderStatus.ProductsUnavailable, ProductIds: missing);
        }

        var lines = products.Select(p => new OrderLine(p, quantities[p.ProductId])).ToList();
        return await CreateOrderAsync(
            new OrderOwner(null, email, IsGuest: true),
            idempotencyKey,
            address,
            lines,
            onCreated: null,
            cancellationToken
        );
    }

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
            // Only tracked here; the SaveChanges below stores it with the status, in one transaction.
            _db.OutboxMessages.Add(
                OutboxMessageFactory.Create(
                    OrderApiOutbox.Producer,
                    OrderEventTypes.OrderCancelled,
                    new OrderCancelled(order.OrderId, order.CustomerEmail, changedAt),
                    changedAt
                )
            );
        }
        // Two statements, one transaction: the status (with its journal row) and the stock are stored together.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        // The order first. A concurrent change of the same order (a double cancel) waits for this row's lock,
        // then fails the xmin check: DbUpdateConcurrencyException, 409, and its stock is never returned.
        await _db.SaveChangesAsync(cancellationToken);

        if (next == OrderStatus.Cancelled)
        {
            // A relative update, units_in_stock = units_in_stock + quantity: it does not depend on a value read
            // earlier, so a checkout running at the same time cannot make it wrong. It changes products.xmin,
            // so that checkout fails its own check (409) instead of writing a stale stock.
            await _db
                .Products.Where(p => p.OrderDetails.Any(d => d.OrderId == orderId))
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(
                            p => p.UnitsInStock,
                            p =>
                                (short)(
                                    p.UnitsInStock
                                    + p.OrderDetails.Where(d => d.OrderId == orderId)
                                        .Select(d => d.Quantity)
                                        .First()
                                )
                        ),
                    cancellationToken
                );
        }

        await transaction.CommitAsync(cancellationToken);
        return ChangeOrderStatusResult.Changed;
    }

    // The checkout of both kinds of buyers once the lines are known: check, store the order, decrement the stock,
    // store the events. onCreated: what else changes in the same transaction (the signed-in user's cart is emptied).
    private async Task<PlaceOrderResult> CreateOrderAsync(
        OrderOwner owner,
        Guid idempotencyKey,
        PlaceOrderRequest address,
        IReadOnlyList<OrderLine> lines,
        Action? onCreated,
        CancellationToken cancellationToken
    )
    {
        var unavailable = lines
            .Where(l => l.Product.Discontinued || l.Product.UnitPrice is null)
            .Select(l => l.Product.ProductId)
            .ToList();
        if (unavailable.Count > 0)
        {
            return new PlaceOrderResult(
                PlaceOrderStatus.ProductsUnavailable,
                ProductIds: unavailable
            );
        }

        var insufficient = lines
            .Where(l => l.Quantity > l.Product.UnitsInStock)
            .Select(l => l.Product.ProductId)
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
        foreach (var (product, lineQuantity) in lines)
        {
            // The quantity fits a smallint: it is at most units_in_stock (checked above).
            var quantity = (short)lineQuantity;
            order.OrderDetails.Add(
                new OrderDetail
                {
                    ProductId = product.ProductId,
                    // A copy of the current price (not null: checked above), so later price changes
                    // do not alter the order.
                    UnitPrice = product.UnitPrice!.Value,
                    ProductName = product.ProductName,
                    Quantity = quantity,
                }
            );
            product.UnitsInStock -= quantity;
        }
        _db.Orders.Add(order);
        onCreated?.Invoke();

        // Two SaveChanges, one transaction: the order (its id comes from the database) and then the events
        // that need this id are committed together or not at all. A product changed since it was read (another buyer,
        // an admin edit) fails the xmin check: DbUpdateConcurrencyException, which the exception handler turns into 409.
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
                            d.UnitPrice
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

    // A product with the quantity to order; long, because a guest's lines for the same product are summed.
    private sealed record OrderLine(Product Product, long Quantity);
}
