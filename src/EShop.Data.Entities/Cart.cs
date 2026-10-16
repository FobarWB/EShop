using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Data.Entities
{
    public class Cart
    {
    [Key]
    public int CartId { get; set; }

    [Required]
    [StringLength(50)]
    public string? UserId { get; set; } = null!;// Communication with the user
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}