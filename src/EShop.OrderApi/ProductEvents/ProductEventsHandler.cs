using EShop.Messaging.Consumers;
using EShop.OrderApi.Data;
using EShop.ProductApi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EShop.OrderApi.ProductEvents;

// Applies ProductApi's snapshots to order_products, as ShoppingCart does to cart_products: one upsert per event that
// writes only if its revision is newer, so duplicates, a republish and reordered deliveries leave the newest snapshot,
// and the row lock of INSERT ... ON CONFLICT orders two deliveries running at the same time.
// A deleted product only gets its tombstone: placed orders keep their own copies of names and prices.
// EF Core has no upsert, hence SQL; the {…} values become parameters.
public sealed class ProductEventsHandler : IMessageHandler
{
    private readonly OrderingDbContext _db;
    private readonly ILogger<ProductEventsHandler> _logger;

    public ProductEventsHandler(OrderingDbContext db, ILogger<ProductEventsHandler> logger)
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
            ProductEventTypes.ProductChanged => ApplyChangedAsync(
                message.Deserialize<ProductChanged>(),
                cancellationToken
            ),
            ProductEventTypes.ProductDeleted => ApplyDeletedAsync(
                message.Deserialize<ProductDeleted>(),
                cancellationToken
            ),
            _ => Task.FromResult(HandleResult.Reject($"its type '{message.Type}' is unknown")),
        };

    private async Task<HandleResult> ApplyChangedAsync(
        ProductChanged changed,
        CancellationToken cancellationToken
    )
    {
        var applied = await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO order_products
                (product_id, product_name, unit_price, units_in_stock, discontinued, deleted, revision)
            VALUES
                ({changed.ProductId}, {changed.ProductName}, {changed.UnitPrice}, {changed.UnitsInStock},
                 {changed.Discontinued}, false, {changed.Revision})
            ON CONFLICT (product_id) DO UPDATE SET
                product_name = excluded.product_name,
                unit_price = excluded.unit_price,
                units_in_stock = excluded.units_in_stock,
                discontinued = excluded.discontinued,
                revision = excluded.revision
            WHERE order_products.revision < excluded.revision AND NOT order_products.deleted
            """,
            cancellationToken
        );
        // 0: an older or the same revision (a duplicate, a republish), or a deleted product.
        _logger.LogInformation(
            "Product {ProductId} revision {Revision}: {Result}",
            changed.ProductId,
            changed.Revision,
            applied == 1 ? "applied" : "ignored"
        );
        return HandleResult.Processed;
    }

    private async Task<HandleResult> ApplyDeletedAsync(
        ProductDeleted deleted,
        CancellationToken cancellationToken
    )
    {
        // The tombstone also when the product was never seen: a ProductChanged delivered later is then ignored.
        var applied = await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO order_products
                (product_id, product_name, unit_price, units_in_stock, discontinued, deleted, revision)
            VALUES ({deleted.ProductId}, '', NULL, 0, false, true, {deleted.Revision})
            ON CONFLICT (product_id) DO UPDATE SET deleted = true, revision = excluded.revision
            WHERE order_products.revision < excluded.revision
            """,
            cancellationToken
        );
        _logger.LogInformation(
            "Product {ProductId} deleted at revision {Revision}: {Result}",
            deleted.ProductId,
            deleted.Revision,
            applied == 1 ? "applied" : "ignored"
        );
        return HandleResult.Processed;
    }
}
