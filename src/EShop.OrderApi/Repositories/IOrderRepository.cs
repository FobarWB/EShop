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

    // email: trimmed and in lower case. The items are summed per product; prices and stock come from the products.
    Task<PlaceOrderResult> PlaceGuestOrderAsync(
        string email,
        Guid idempotencyKey,
        PlaceOrderRequest address,
        IReadOnlyList<GuestOrderItem> items,
        CancellationToken cancellationToken
    );

    // The guest order of an earlier try with this key and email, or null.
    Task<OrderResponse?> RetrieveGuestOrderAsync(
        string email,
        Guid idempotencyKey,
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
