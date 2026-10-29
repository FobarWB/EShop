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
        // A retry of an attempt that already succeeded (double click, lost response): the same order again.
        var existing = await RetrieveOrderAsync(userId, idempotencyKey, cancellationToken);
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
        var items = cart.CartItems.Select(i => (Item: i, Product: i.Product!)).ToList();

        var unavailable = items
            .Where(x => x.Product.Discontinued || x.Product.UnitPrice is null)
            .Select(x => x.Item.ProductId)
            .ToList();
        if (unavailable.Count > 0)
        {
            return new PlaceOrderResult(
                PlaceOrderStatus.ProductsUnavailable,
                ProductIds: unavailable
            );
        }

        var insufficient = items
            .Where(x => x.Item.Quantity > x.Product.UnitsInStock)
            .Select(x => x.Item.ProductId)
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
            UserId = userId,
            CustomerEmail = customerEmail,
            IdempotencyKey = idempotencyKey,
            OrderDate = now,
            Freight = StandardFreight,
            ShipName = request.ShipName,
            ShipAddress = request.ShipAddress,
            ShipCity = request.ShipCity,
            ShipRegion = request.ShipRegion,
            ShipPostalCode = request.ShipPostalCode,
            ShipCountry = request.ShipCountry,
        };
        order.RecordPlaced(userId, now);
        foreach (var (item, product) in items)
        {
            // The quantity fits a smallint: it is at most units_in_stock (checked above).
            var quantity = (short)item.Quantity;
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
        cart.CartItems.Clear();

        // One SaveChanges is one transaction: the order, the stock and the emptied cart are stored together or not
        // at all. A product changed since it was read (another buyer, an admin edit) fails the xmin check:
        // DbUpdateConcurrencyException, which the exception handler turns into 409.
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: IdempotencyKeyIndex,
                    }
            )
        {
            // A concurrent request with the same key placed the order between the check above and this save.
            var placed = await RetrieveOrderAsync(userId, idempotencyKey, cancellationToken);
            return new PlaceOrderResult(PlaceOrderStatus.AlreadyPlaced, placed);
        }

        var created = await RetrieveOrderAsync(userId, order.OrderId, cancellationToken);
        return new PlaceOrderResult(PlaceOrderStatus.Placed, created);
    }

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

    private Task<OrderResponse?> RetrieveOrderAsync(
        string userId,
        Guid idempotencyKey,
        CancellationToken cancellationToken
    ) =>
        _db
            .Orders.Where(o => o.UserId == userId && o.IdempotencyKey == idempotencyKey)
            .ToResponse()
            .FirstOrDefaultAsync(cancellationToken);
}
