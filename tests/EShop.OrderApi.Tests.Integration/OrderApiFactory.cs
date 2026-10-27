using EShop.Tests.Common;

namespace EShop.OrderApi.Tests.Integration;

// A class fixture: one in-memory API per test class, using this project's test database.
public sealed class OrderApiFactory(OrderApiDatabase database) : EShopApiFactory<Program>(database);
