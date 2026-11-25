namespace EShop.Persistence;

/// <summary>
/// A DbContext owned by one service (or one bounded context): its tables and its migrations history
/// live in the PostgreSQL schema <see cref="Schema"/>.
/// </summary>
public interface IServiceDbContext
{
    static abstract string Schema { get; }
}
