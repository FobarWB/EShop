namespace EShop.Persistence;

/// <summary>
/// A DbContext owned by one service (or one bounded context): its tables and its migrations history
/// live in the PostgreSQL schema <see cref="Schema"/> of its own database, read from
/// <c>ConnectionStrings:&lt;ConnectionStringName&gt;</c>.
/// </summary>
public interface IServiceDbContext
{
    static abstract string Schema { get; }

    // One database and role per bounded context (#78): ConnectionStrings:Catalog, Cart, Ordering, Customer, Notification.
    static abstract string ConnectionStringName { get; }
}
