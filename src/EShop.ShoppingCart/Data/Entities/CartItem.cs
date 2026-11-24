using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EShop.ShoppingCart.Data.Entities;

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

    // The product is the cart's copy, cart_products (#75): a foreign key, no navigation.
}
