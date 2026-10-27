using EShop.ServiceDefaults;
using EShop.ShoppingCart.Dtos;
using EShop.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShop.ShoppingCart.Controllers;

[ApiController]
[Route("api/carts/me")]
// Every action works on the signed-in user's cart: the user id comes from the token, never from the request.
[Authorize]
public class ShoppingCartController : ControllerBase
{
    private readonly IShoppingCartRepository _repository;
    private readonly ILogger<ShoppingCartController> _logger;

    public ShoppingCartController(
        IShoppingCartRepository repository,
        ILogger<ShoppingCartController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    private string UserId => User.GetUserId();

    // POST api/carts/me/items
    [HttpPost("items")]
    public async Task<IActionResult> AddItemToCart(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken
    )
    {
        // Named fields only: the request body itself is never logged.
        _logger.LogInformation("Adding product {ProductId} to the cart", request.ProductId);

        var result = await _repository.AddItemToCartAsync(
            UserId,
            request.ProductId,
            request.Quantity,
            cancellationToken
        );
        return result switch
        {
            AddItemResult.ProductNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product not found"
            ),
            AddItemResult.InsufficientStock => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Not enough units in stock"
            ),
            _ => Ok(new { message = "Item added to cart." }),
        };
    }

    // GET api/carts/me
    [HttpGet]
    public async Task<ActionResult<CartResponse>> GetCart(CancellationToken cancellationToken)
    {
        var cart = await _repository.RetrieveCartAsync(UserId, cancellationToken);
        return cart is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Cart not found")
            : cart;
    }

    // PUT api/carts/me/items/1
    [HttpPut("items/{productId}")]
    public async Task<IActionResult> UpdateItemQuantity(
        int productId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken
    )
    {
        var updated = await _repository.UpdateItemQuantityAsync(
            UserId,
            productId,
            request.Quantity,
            cancellationToken
        );
        return updated
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Failed to update item quantity"
            );
    }

    // DELETE api/carts/me/items/1
    [HttpDelete("items/{productId}")]
    public async Task<IActionResult> RemoveItemFromCart(
        int productId,
        CancellationToken cancellationToken
    )
    {
        var removed = await _repository.RemoveItemFromCartAsync(
            UserId,
            productId,
            cancellationToken
        );
        return removed
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Failed to remove item from cart"
            );
    }

    // DELETE api/carts/me/items
    [HttpDelete("items")]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken)
    {
        var cleared = await _repository.ClearCartAsync(UserId, cancellationToken);
        return cleared
            ? Ok()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Failed to clear cart");
    }

    // DELETE api/carts/me
    [HttpDelete]
    public async Task<IActionResult> DeleteCart(CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteCartAsync(UserId, cancellationToken);
        if (!result.HasValue)
        {
            _logger.LogWarning("Cart not found for userId: {UserId}", UserId);
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Cart not found");
        }
        if (!result.Value)
        {
            _logger.LogError("Failed to delete cart for userId: {UserId}", UserId);
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Failed to delete cart"
            );
        }
        return Ok();
    }
}
