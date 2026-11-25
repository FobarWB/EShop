using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Catalog.Data.Entities;

// A Northwind table that stays with the catalog: products reference it.
[Index("CompanyName")]
[Index("PostalCode")]
public partial class Supplier
{
    public Supplier()
    {
        Products = new HashSet<Product>();
    }

    [Key]
    public int SupplierId { get; set; }

    [StringLength(40)]
    public string CompanyName { get; set; } = null!;

    [StringLength(30)]
    public string? ContactName { get; set; }

    [StringLength(30)]
    public string? ContactTitle { get; set; }

    [StringLength(60)]
    public string? Address { get; set; }

    [StringLength(15)]
    public string? City { get; set; }

    [StringLength(15)]
    public string? Region { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    [StringLength(15)]
    public string? Country { get; set; }

    [StringLength(24)]
    public string? Phone { get; set; }

    [StringLength(24)]
    public string? Fax { get; set; }
    public string? HomePage { get; set; }

    [InverseProperty("Supplier")]
    public virtual ICollection<Product> Products { get; set; }
}
