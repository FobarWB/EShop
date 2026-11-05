using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EShop.Data.Entities;

// Shop data of a Keycloak user, keyed by the token's sub. Email and name are not copied: the token carries them.
// Created on the user's first request to the customer API (just-in-time).
public class CustomerProfile
{
    [Key]
    [StringLength(50)]
    public string UserId { get; set; } = null!;

    [StringLength(24)]
    public string? Phone { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime UpdatedAt { get; set; }

    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
}
