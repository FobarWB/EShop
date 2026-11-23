using EShop.Data.Entities;
using EShop.OrderApi.Dtos;
using EShop.ServiceDefaults;

namespace EShop.OrderApi.Repositories;

public interface IOrderRepository
{
    // The Pending order and its order.placed (#76): ProductApi reserves the stock and answers. The items are summed
    // per product; names, prices and the stock hint come from the catalog's replica (order_products).
    Task<PlaceOrderResult> PlaceOrderAsync(
        string userId,
        string? customerEmail,
        Guid idempotencyKey,
        PlaceOrderRequest request,
        CancellationToken cancellationToken
    );

    // As PlaceOrderAsync, for a buyer without an account. email: trimmed and in lower case.
    Task<PlaceOrderResult> PlaceGuestOrderAsync(
        string email,
        Guid idempotencyKey,
        ShippingAddressRequest address,
        IReadOnlyList<OrderItemRequest> items,
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

    // ownerId null: any order (an admin). The stock of a cancelled order is returned by ProductApi on order.cancelled.
    Task<ChangeOrderStatusResult> ChangeStatusAsync(
        int orderId,
        string? ownerId,
        OrderStatus next,
        string changedBy,
        CancellationToken cancellationToken
    );
}
