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
            if (cartRequest == null)
            {
                return BadRequest("Cart request cannot be null.");
            }

            // Check if cartItems is initialized
            if (cartRequest.CartItems == null)
            {
                return BadRequest("Cart items cannot be null.");
            }

            Cart cart = new Cart
            {
                UserId = cartRequest.UserId,
                CreatedAt = DateTime.UtcNow, 
                CartItems = cartRequest.CartItems
            };

            var createdCart = await _repository.CreateCartAsync(cart);
            
            if (createdCart == null)
                return BadRequest("Failed to create cart");

            return Ok(createdCart);
        }

        // POST: api/carts/userId/add
        [HttpPost("{userId}/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItemToCart(string userId, [FromBody] CartItem item)
        {
            var result = await _repository.AddItemToCartAsync(userId, item);
            if (!result) return BadRequest("Failed to add item to cart");
            return Ok("Item added to cart.");
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
            var result = await _repository.UpdateItemQuantityAsync(userId, productId, newQuantity);
            if (!result) return BadRequest("Failed to update item quantity");
            return Ok("Item quantity updated.");
        }

        //DELETE: api/carts/userid/remove/productId
        [HttpDelete("{userId}/remove/{productId}")]
        public async Task<IActionResult> RemoveItemFromCart(string userId, int productId)
        {
            var result = await _repository.RemoveItemFromCartAsync(userId, productId);
            if (!result) return BadRequest("Failed to remove item from cart");
            return Ok();
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
            if (!result.HasValue) return NotFound("Cart not found");
            if (!result.Value) return BadRequest("Failed to delete cart");
            return Ok();
        }
    }
}