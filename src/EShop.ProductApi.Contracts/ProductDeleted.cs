namespace EShop.ProductApi.Contracts;

// Published when a product is deleted. Revision is the last revision + 1: a consumer keeps it with a tombstone,
// so a ProductChanged delivered late cannot bring the product back.
public sealed record ProductDeleted(int ProductId, long Revision, DateTime DeletedAt);
