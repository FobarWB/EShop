using EShop.ShoppingCart.Data;
using EShop.Tests.Common;

namespace EShop.ShoppingCart.Tests.Integration;

// A class fixture: one in-memory API per test class, using this project's test database.
public sealed class ShoppingCartApiFactory(ShoppingCartDatabase database)
    : EShopApiFactory<Program, CartDbContext>(database);
