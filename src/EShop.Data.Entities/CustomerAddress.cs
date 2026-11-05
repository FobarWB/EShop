using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EShop.Data.Entities;

// A saved shipping address. The lengths are those of the Northwind orders columns, so a saved address
// always fits into an order.
public class CustomerAddress
{
    [Key]
    public int AddressId { get; set; }

    [StringLength(50)]
    public string UserId { get; set; } = null!;

    [StringLength(40)]
    public string RecipientName { get; set; } = null!;

    [StringLength(60)]
    public string Address { get; set; } = null!;

    [StringLength(15)]
    public string City { get; set; } = null!;

    [StringLength(15)]
    public string? Region { get; set; }

    [StringLength(10)]
    public string PostalCode { get; set; } = null!;

    [StringLength(15)]
    public string Country { get; set; } = null!;

    // Prefilled at checkout; at most one per user (partial unique index).
    public bool IsDefault { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    public CustomerProfile Profile { get; set; } = null!;
}
