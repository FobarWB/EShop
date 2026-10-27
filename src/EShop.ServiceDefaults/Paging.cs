using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace EShop.ServiceDefaults;

// Query string of a collection endpoint: ?page=2&pageSize=20. Bind it as [FromQuery] PageRequest paging:
// a parameter named "page" would make MVC look for page.Page and silently fall back to the defaults.
public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public static class PagingExtensions
{
    // Offset paging: COUNT(*), then OFFSET/LIMIT. Fine for a catalogue of this size; large tables would
    // use keyset paging (WHERE id > last id) instead. The query must be ordered: OFFSET without ORDER BY
    // may return a row on two pages or on none.
    public static async Task<PagedResponse<T>> ToPageAsync<T>(
        this IQueryable<T> query,
        PageRequest paging,
        CancellationToken cancellationToken
    )
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, paging.Page, paging.PageSize, totalCount);
    }
}
