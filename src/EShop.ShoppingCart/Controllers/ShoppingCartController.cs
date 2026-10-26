using System.ComponentModel.DataAnnotations;
using EShop.Data.Entities;
using EShop.ServiceDefaults;
using EShop.ShoppingCart.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShop.ShoppingCart.Controllers
{
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

        // POST: api/carts/me/items
        [HttpPost("items")]
        public async Task<IActionResult> AddItemToCart(
            [FromBody] CartItem item,
            CancellationToken cancellationToken
        )
        {
            _logger.LogInformation("Received Cart Request: {@CartRequest}", item);
            if (item.ProductId == 0 || item.Quantity <= 0)
            {
                return BadRequest("Invalid item data.");
            }

            var result = await _repository.AddItemToCartAsync(UserId, item, cancellationToken);
            return result switch
            {
                AddItemResult.ProductNotFound => NotFound("Product not found."),
                AddItemResult.InsufficientStock => Conflict("Not enough units in stock."),
                _ => Ok(new { message = "Item added to cart." }),
            };
        }

        // GET: api/carts/me
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var cart = await _repository.RetrieveCartByUserIdAsync(UserId);
            if (cart == null)
            {
                return NotFound("Cart not found");
            }

            return Ok(cart);
        }

        // PUT: api/carts/me/items/productId
        [HttpPut("items/{productId}")]
        public async Task<IActionResult> UpdateItemQuantity(
            int productId,
            [FromBody, Range(1, int.MaxValue)] int newQuantity
        )
        {
            var result = await _repository.UpdateItemQuantityAsync(UserId, productId, newQuantity);
            if (!result)
            {
                return BadRequest("Failed to update item quantity");
            }
            return NoContent();
        }

        // DELETE: api/carts/me/items/productId
        [HttpDelete("items/{productId}")]
        public async Task<IActionResult> RemoveItemFromCart(int productId)
        {
            var result = await _repository.RemoveItemFromCartAsync(UserId, productId);
            if (!result)
            {
                return BadRequest("Failed to remove item from cart");
            }

            return NoContent();
        }

        // DELETE: api/carts/me/items
        [HttpDelete("items")]
        public async Task<IActionResult> ClearCart()
        {
            var result = await _repository.ClearCartAsync(UserId);
            if (!result)
            {
                return BadRequest("Failed to clear cart");
            }

            return Ok();
        }

        // DELETE: api/carts/me
        [HttpDelete]
        public async Task<IActionResult> DeleteCart()
        {
            var result = await _repository.DeleteCartAsync(UserId);
            if (!result.HasValue)
            {
                _logger.LogWarning("Cart not found for userId: {UserId}", UserId);
                return NotFound("Cart not found");
            }
            if (!result.Value)
            {
                _logger.LogError("Failed to delete cart for userId: {UserId}", UserId);
                return BadRequest("Failed to delete cart");
            }
            return Ok();
        }
    }
}
