using EShop.Tests.Common;

namespace EShop.ProductApi.Tests.Integration;

// A class fixture: one in-memory API per test class, using this project's test database.
public sealed class ProductApiFactory(ProductApiDatabase database)
    : EShopApiFactory<Program>(database);
