namespace EShop.Data.Entities;

// The order lifecycle: the only place that knows the allowed transitions.
//   Pending → Paid → Shipped → Delivered
//   Pending, Paid → Cancelled
public partial class Order
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
    };

    public bool CanChangeStatusTo(OrderStatus next) =>
        AllowedTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(next);

    // Called once by checkout: the journal starts with the creation of the order.
    public void RecordPlaced(string changedBy, DateTime at) =>
        StatusChanges.Add(
            new OrderStatusChange
            {
                ToStatus = Status,
                ChangedAt = at,
                ChangedBy = changedBy,
            }
        );

    // false for a transition the lifecycle does not allow: the caller answers 409.
    // Side effects outside the order (returning the stock on cancel) are the repository's job.
    public bool TryChangeStatus(OrderStatus next, string changedBy, DateTime at)
    {
        if (!CanChangeStatusTo(next))
        {
            return false;
        }

        StatusChanges.Add(
            new OrderStatusChange
            {
                FromStatus = Status,
                ToStatus = next,
                ChangedAt = at,
                ChangedBy = changedBy,
            }
        );
        Status = next;
        if (next == OrderStatus.Shipped)
        {
            ShippedDate = at;
        }
        return true;
    }
}
