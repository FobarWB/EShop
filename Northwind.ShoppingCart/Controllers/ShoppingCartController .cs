using EShop.Shared;
using Microsoft.AspNetCore.Mvc;
using Northwind.ShoppingCart.Repositories;

namespace Northwind.ShoppingCart.Controllers
{
    [ApiController]
    [Route("api/carts")]
    public class ShoppingCartController : ControllerBase
    {
        private readonly IShoppingCartRepository _repository;
        private readonly ILogger<ShoppingCartController> _logger;

        public ShoppingCartController(IShoppingCartRepository repository, ILogger<ShoppingCartController> logger)
        {
            _repository = repository;
             _logger = logger;
        }

        // POST: api/carts/create
        [HttpPost("create")]
        public async Task<IActionResult> CreateCart([FromBody] Cart cartRequest)
        {
            _logger.LogInformation("Received Cart Request: {@CartRequest}", cartRequest);
            
            // Проверка на null
            if (cartRequest == null)
            {
                return BadRequest("Cart request cannot be null.");
            }

            // Проверка на наличие UserId
            if (string.IsNullOrEmpty(cartRequest.UserId))
            {
                return BadRequest("UserId cannot be null or empty.");
            }

            // Создание объекта корзины
            Cart cart = new Cart
            {
                UserId = cartRequest.UserId,
                CreatedAt = DateTime.UtcNow,
                CartItems = new List<CartItem>() // Создаем пустой список элементов корзины
            };

            try
            {
                var createdCart = await _repository.CreateCartAsync(cart);
                if (createdCart == null)
                {
                    return BadRequest("Failed to create cart");
                }

                return Ok(createdCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating cart.");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/carts/userId/add
        [HttpPost("{userId}/add")]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItemToCart(string userId, [FromBody] CartItem item)
        {
            _logger.LogInformation("Received Cart Request: {@CartRequest}", item);
            if (item == null || item.ProductId == 0 || item.Quantity <= 0)
            {
                return BadRequest("Invalid item data.");
            }

            var result = await _repository.AddItemToCartAsync(userId, item);
            if (!result)
            {
                return BadRequest("Failed to add item to cart");
            }
            Console.WriteLine($"UserId: {userId}, ProductId: {item.ProductId}, Quantity: {item.Quantity}");
            return Ok(new { message = "Item added to cart." });
        }

        // GET: api/carts/userId
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetCartByUserId(string userId)
        {
            var cart = await _repository.RetrieveCartByUserIdAsync(userId);
            if (cart == null) return NotFound("Cart not found");
            return Ok(cart);
        }

        // PUT: api/carts/userId/update/productId
        [HttpPut("{userId}/update/{productId}")]
        public async Task<IActionResult> UpdateItemQuantity(string userId, int productId, [FromBody] int newQuantity)
        {
            System.Console.WriteLine(newQuantity);
            var result = await _repository.UpdateItemQuantityAsync(userId, productId, newQuantity);
            if (!result) return BadRequest("Failed to update item quantity");
            // return Ok(new { message = "Item quantity updated." });
            return NoContent();
        }

        //DELETE: api/carts/userid/remove/productId
        [HttpDelete("{userId}/remove/{productId}")]
        public async Task<IActionResult> RemoveItemFromCart(string userId, int productId)
        {
            var result = await _repository.RemoveItemFromCartAsync(userId, productId);
            if (!result) return BadRequest("Failed to remove item from cart");
            Console.WriteLine($"Delete item from cart by {userId}");
            return NoContent();
        }

        // DELETE: api/carts/userId/clear
        [HttpDelete("{userId}/clear")]
        public async Task<IActionResult> ClearCart(string userId)
        {
            var result = await _repository.ClearCartAsync(userId);
            if (!result) return BadRequest("Failed to clear cart");
            return Ok();
        }

        // DELETE: api/carts/userId
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteCart(string userId)
        {
            var result = await _repository.DeleteCartAsync(userId);
            if (!result.HasValue)
            {
                _logger.LogWarning("Cart not found for userId: {UserId}", userId);
                return NotFound("Cart not found");
            }
            if (!result.Value)
            {
                _logger.LogError("Failed to delete cart for userId: {UserId}", userId);
                return BadRequest("Failed to delete cart");
            }
            return Ok();
        }
    }
}