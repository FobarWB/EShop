using EShop.Data.Entities;
using EShop.OrderApi.Dtos;
using EShop.ServiceDefaults;

namespace EShop.OrderApi.Repositories;

public interface IOrderRepository
{
    Task<PlaceOrderResult> PlaceOrderAsync(
        string userId,
        string? customerEmail,
        Guid idempotencyKey,
        PlaceOrderRequest request,
        CancellationToken cancellationToken
    );

    // null: no such order, or it belongs to another user.
    Task<OrderResponse?> RetrieveOrderAsync(
        string userId,
        int orderId,
        CancellationToken cancellationToken
    );

    // The user's orders, newest first.
    Task<PagedResponse<OrderSummaryResponse>> RetrieveOrdersAsync(
        string userId,
        PageRequest paging,
        CancellationToken cancellationToken
    );

    // ownerId null: any order (an admin). Cancelling returns the stock in the same transaction.
    Task<ChangeOrderStatusResult> ChangeStatusAsync(
        int orderId,
        string? ownerId,
        OrderStatus next,
        string changedBy,
        CancellationToken cancellationToken
    );
}
