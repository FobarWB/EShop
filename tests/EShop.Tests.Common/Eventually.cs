namespace EShop.Tests.Common;

/// <summary>
/// Waits for something that happens in the background (a consumer, the outbox publisher):
/// polls the condition until it holds or the timeout passes.
/// </summary>
public static class Eventually
{
    public static async Task<bool> HoldsAsync(
        Func<Task<bool>> condition,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null
    )
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }
            await Task.Delay(50, cancellationToken);
        }
        return await condition();
    }
}
