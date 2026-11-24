using EShop.Messaging.Consumers;
using EShop.ProductApi.Contracts;
using EShop.ShoppingCart.Data;
using Microsoft.EntityFrameworkCore;

namespace EShop.ShoppingCart.ProductEvents;

// Applies ProductApi's snapshots to cart_products. Each event is one upsert that writes only if its revision is newer
// than the stored one: duplicates, a republish and reordered deliveries leave the newest snapshot. One statement, no
// read before the write, so two deliveries running at the same time cannot both pass a check: the row lock of
// INSERT ... ON CONFLICT orders them, and the second compares with the first one's revision.
// The statements run inside the consumer's transaction, together with the inbox row.
// EF Core has no upsert, hence SQL; the {…} values become parameters.
public sealed class ProductEventsHandler : IMessageHandler
{
    private readonly CartDbContext _db;
    private readonly ILogger<ProductEventsHandler> _logger;

    public ProductEventsHandler(CartDbContext db, ILogger<ProductEventsHandler> logger)
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
            INSERT INTO cart_products
                (product_id, product_name, category_id, unit_price, units_in_stock, discontinued, deleted, revision)
            VALUES
                ({changed.ProductId}, {changed.ProductName}, {changed.CategoryId}, {changed.UnitPrice},
                 {changed.UnitsInStock}, {changed.Discontinued}, false, {changed.Revision})
            ON CONFLICT (product_id) DO UPDATE SET
                product_name = excluded.product_name,
                category_id = excluded.category_id,
                unit_price = excluded.unit_price,
                units_in_stock = excluded.units_in_stock,
                discontinued = excluded.discontinued,
                revision = excluded.revision
            WHERE cart_products.revision < excluded.revision AND NOT cart_products.deleted
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
        await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO cart_products
                (product_id, product_name, category_id, unit_price, units_in_stock, discontinued, deleted, revision)
            VALUES ({deleted.ProductId}, '', NULL, NULL, 0, false, true, {deleted.Revision})
            ON CONFLICT (product_id) DO UPDATE SET deleted = true, revision = excluded.revision
            WHERE cart_products.revision < excluded.revision
            """,
            cancellationToken
        );
        // Whatever the revision: a deleted product never comes back, so it leaves every cart.
        var removed = await _db.Database.ExecuteSqlAsync(
            $"DELETE FROM cart_items WHERE product_id = {deleted.ProductId}",
            cancellationToken
        );
        _logger.LogInformation(
            "Product {ProductId} deleted; removed from {Count} carts",
            deleted.ProductId,
            removed
        );
        return HandleResult.Processed;
    }
}
