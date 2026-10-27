using EShop.Data;
using EShop.Data.Entities;
using EShop.OrderApi.Dtos;
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

        var order = new Order
        {
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            // order_date has no time zone: the API stores UTC there, and Npgsql refuses a DateTime of kind Utc.
            OrderDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Freight = StandardFreight,
            ShipName = request.ShipName,
            ShipAddress = request.ShipAddress,
            ShipCity = request.ShipCity,
            ShipRegion = request.ShipRegion,
            ShipPostalCode = request.ShipPostalCode,
            ShipCountry = request.ShipCountry,
        };
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
