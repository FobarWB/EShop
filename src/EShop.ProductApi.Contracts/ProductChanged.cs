namespace EShop.ProductApi.Contracts;

// A snapshot of the product after a change, not a description of the change: a consumer replaces its copy with it
// and does not care what changed. Revision grows by one with every change of the product (products.revision, written
// under the row lock, so its order is the commit order); a consumer ignores a snapshot not newer than its copy,
// which makes duplicates and reordered deliveries harmless.
public sealed record ProductChanged(
    int ProductId,
    string ProductName,
    int? CategoryId,
    decimal? UnitPrice,
    short UnitsInStock,
    bool Discontinued,
    long Revision,
    DateTime ChangedAt
);
