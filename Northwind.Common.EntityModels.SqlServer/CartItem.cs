using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Shared
{
    public class CartItem
    {
    [Key]
    public int CartItemId { get; set; }
    [ForeignKey("Cart")]
    public int CartId { get; set; } // External key for communication with Cart
    [Required]
    public long ProductId { get; set; }
    [Required]
    [StringLength(100)]
    public string ProductName { get; set; } = null!;
    [Required]
    public int Quantity { get; set; }
    [Column(TypeName = "money")]
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;
    public string? Image { get; set; }

    [ForeignKey("CartId")]
    public virtual Cart? Cart { get; set; } // Communication/linking with the shopping cart
    }
}