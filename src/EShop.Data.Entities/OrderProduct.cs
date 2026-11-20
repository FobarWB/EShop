using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace EShop.Data.Entities;

// OrderApi's copy of a product (#76): checkout prices an order from it instead of reading `products`.
// Revision: the one of the last event applied; an event not newer is ignored. Deleted: a tombstone, so a
// ProductChanged delivered after the ProductDeleted cannot bring the product back. UnitsInStock is a hint for an
// early answer at checkout; ProductApi's reservation decides.
public class OrderProduct
{
    [Key]
    public int ProductId { get; set; }

    [StringLength(40)]
    public string ProductName { get; set; } = null!;

    [Precision(19, 4)]
    public decimal? UnitPrice { get; set; }
    public short UnitsInStock { get; set; }
    public bool Discontinued { get; set; }
    public bool Deleted { get; set; }
    public long Revision { get; set; }
}
