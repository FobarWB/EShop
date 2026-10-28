namespace EShop.OrderApi.Repositories;

public enum ChangeOrderStatusResult
{
    Changed,

    // No such order, or (for a buyer) another user's order.
    NotFound,

    // The lifecycle does not allow this transition from the current status.
    NotAllowed,
}
