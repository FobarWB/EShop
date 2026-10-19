using EShop.Tests.Common;

namespace EShop.CategoryApi.Tests.Integration;

// A class fixture: one in-memory API per test class, using this project's test database.
public sealed class CategoryApiFactory(CategoryApiDatabase database)
    : EShopApiFactory<Program>(database);
