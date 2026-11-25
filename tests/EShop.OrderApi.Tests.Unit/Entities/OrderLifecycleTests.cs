using EShop.OrderApi.Data.Entities;

namespace EShop.OrderApi.Tests.Unit.Entities;

public class OrderLifecycleTests
{
    private const string UserId = "user-1";
    private static readonly DateTime At = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private static readonly HashSet<(OrderStatus, OrderStatus)> Allowed =
    [
        (OrderStatus.Pending, OrderStatus.Confirmed),
        (OrderStatus.Pending, OrderStatus.Rejected),
        (OrderStatus.Pending, OrderStatus.Cancelled),
        (OrderStatus.Confirmed, OrderStatus.Paid),
        (OrderStatus.Confirmed, OrderStatus.Cancelled),
        (OrderStatus.Paid, OrderStatus.Shipped),
        (OrderStatus.Paid, OrderStatus.Cancelled),
        (OrderStatus.Shipped, OrderStatus.Delivered),
    ];

    // Every pair of statuses, 49 cases: the eight transitions above are allowed, all others are not.
    public static TheoryData<OrderStatus, OrderStatus, bool> AllPairs()
    {
        var data = new TheoryData<OrderStatus, OrderStatus, bool>();
        foreach (var from in Enum.GetValues<OrderStatus>())
        foreach (var to in Enum.GetValues<OrderStatus>())
        {
            data.Add(from, to, Allowed.Contains((from, to)));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void TryChangeStatus_AnyPair_FollowsLifecycle(
        OrderStatus from,
        OrderStatus to,
        bool allowed
    )
    {
        var order = OrderIn(from);
        var journal = order.StatusChanges.Count;

        var changed = order.TryChangeStatus(to, UserId, At);

        changed.ShouldBe(allowed);
        order.Status.ShouldBe(allowed ? to : from);
        order.StatusChanges.Count.ShouldBe(journal + (allowed ? 1 : 0));
    }

    [Fact]
    public void TryChangeStatus_Allowed_RecordsTheChange()
    {
        var order = OrderIn(OrderStatus.Pending);

        order.TryChangeStatus(OrderStatus.Cancelled, UserId, At);

        var change = order.StatusChanges.ShouldHaveSingleItem();
        change.FromStatus.ShouldBe(OrderStatus.Pending);
        change.ToStatus.ShouldBe(OrderStatus.Cancelled);
        change.ChangedBy.ShouldBe(UserId);
        change.ChangedAt.ShouldBe(At);
    }

    [Fact]
    public void TryChangeStatus_ToShipped_SetsShippedDate()
    {
        var order = OrderIn(OrderStatus.Paid);

        order.TryChangeStatus(OrderStatus.Shipped, UserId, At);

        order.ShippedDate.ShouldBe(At);
    }

    [Fact]
    public void RecordPlaced_StartsJournalWithoutFromStatus()
    {
        var order = new Order();

        order.RecordPlaced(UserId, At);

        var change = order.StatusChanges.ShouldHaveSingleItem();
        change.FromStatus.ShouldBeNull();
        change.ToStatus.ShouldBe(OrderStatus.Pending);
    }

    // Walks the lifecycle to the status (Status has no public setter), then forgets the journal of the walk.
    private static Order OrderIn(OrderStatus status)
    {
        var order = new Order();
        OrderStatus[] path = status switch
        {
            OrderStatus.Pending => [],
            OrderStatus.Confirmed => [OrderStatus.Confirmed],
            OrderStatus.Paid => [OrderStatus.Confirmed, OrderStatus.Paid],
            OrderStatus.Shipped => [OrderStatus.Confirmed, OrderStatus.Paid, OrderStatus.Shipped],
            OrderStatus.Delivered =>
            [
                OrderStatus.Confirmed,
                OrderStatus.Paid,
                OrderStatus.Shipped,
                OrderStatus.Delivered,
            ],
            OrderStatus.Cancelled => [OrderStatus.Cancelled],
            OrderStatus.Rejected => [OrderStatus.Rejected],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        foreach (var next in path)
        {
            order.TryChangeStatus(next, "setup", At);
        }
        order.StatusChanges.Clear();
        return order;
    }
}
