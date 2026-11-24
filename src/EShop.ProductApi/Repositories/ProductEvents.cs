using EShop.Catalog.Data;
using EShop.Catalog.Data.Entities;
using EShop.Messaging.Outbox;
using EShop.ProductApi.Contracts;

namespace EShop.ProductApi.Repositories;

// Every change of a product goes through here: the revision grows by one and the event is added to the outbox of the
// same DbContext, so the next SaveChanges stores the change and its event together, or neither.
public static class ProductEvents
{
    // The product is tracked; its ProductId must be known (after the INSERT for a new product).
    public static void AddChanged(CatalogDbContext db, Product product, DateTime changedAt)
    {
        product.Revision++;
        db.OutboxMessages.Add(
            OutboxMessageFactory.Create(
                ProductApiOutbox.Producer,
                ProductEventTypes.ProductChanged,
                ToChanged(product, changedAt),
                changedAt
            )
        );
    }

    public static void AddDeleted(CatalogDbContext db, Product product, DateTime deletedAt) =>
        db.OutboxMessages.Add(
            OutboxMessageFactory.Create(
                ProductApiOutbox.Producer,
                ProductEventTypes.ProductDeleted,
                new ProductDeleted(product.ProductId, product.Revision + 1, deletedAt),
                deletedAt
            )
        );

    // The current state again, without a new revision: for the republish, consumers that have it ignore it.
    public static void AddSnapshot(CatalogDbContext db, Product product, DateTime at) =>
        db.OutboxMessages.Add(
            OutboxMessageFactory.Create(
                ProductApiOutbox.Producer,
                ProductEventTypes.ProductChanged,
                ToChanged(product, at),
                at
            )
        );

    private static ProductChanged ToChanged(Product product, DateTime at) =>
        new(
            product.ProductId,
            product.ProductName,
            product.CategoryId,
            product.UnitPrice,
            product.UnitsInStock,
            product.Discontinued,
            product.Revision,
            at
        );
}
