using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Shared
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

    [ForeignKey("ProductId")]
    public virtual Product? Product { get; set; } 
    }
}