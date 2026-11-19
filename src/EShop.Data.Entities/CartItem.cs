using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Data.Entities
{
    public class CartItem
    {
    [Key, Column(Order = 0)]
    public int CartId { get; set; } //Compound key
    [Key, Column(Order = 1)]
    public int ProductId { get; set; } //Compound key
    [Required]
    public int Quantity { get; set; }

    [ForeignKey("CartId")]
    public virtual Cart? Cart { get; set; } // Communication/linking with the shopping cart

    // No navigation to the product: the cart has its own copy, cart_products (#75), joined by product_id.
    }
}
