using EShop.OrderApi.Dtos;

namespace EShop.OrderApi.Repositories;

public interface IOrderRepository
{
    Task<PlaceOrderResult> PlaceOrderAsync(
        string userId,
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
}
