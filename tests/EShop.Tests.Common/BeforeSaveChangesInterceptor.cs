using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EShop.Tests.Common;

/// <summary>
/// Runs an action once, right before the next SaveChanges sends its commands: simulates a concurrent request
/// that changes the database between a repository's check and its write.
/// </summary>
public sealed class BeforeSaveChangesInterceptor(Func<Task> action) : SaveChangesInterceptor
{
    private Func<Task>? _action = action;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (Interlocked.Exchange(ref _action, null) is { } action)
        {
            await action();
        }
        return result;
    }
}
