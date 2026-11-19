using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace EShop.Data.Entities;

// ShoppingCart's copy of a product (#75), kept up to date by ProductApi's events instead of reading `products`.
// Revision: the one of the last event applied; an event not newer is ignored. Deleted: a tombstone, so a
// ProductChanged delivered after the ProductDeleted cannot bring the product back. The cart never shows a
// deleted row; its other columns are the last known values (empty for a product deleted before it was ever seen).
public class CartProduct
{
    [Key]
    public int ProductId { get; set; }

    [StringLength(40)]
    public string ProductName { get; set; } = null!;
    public int? CategoryId { get; set; }

    [Precision(19, 4)]
    public decimal? UnitPrice { get; set; }
    public short UnitsInStock { get; set; }
    public bool Discontinued { get; set; }
    public bool Deleted { get; set; }
    public long Revision { get; set; }
}
