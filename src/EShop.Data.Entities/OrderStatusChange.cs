using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EShop.Data.Entities;

// Append-only journal of an order's status: one row per transition, the creation included (FromStatus null).
// Rows are never updated or deleted; Northwind orders have no rows (no real history to record).
public class OrderStatusChange
{
    [Key]
    public long OrderStatusChangeId { get; set; }

    public int OrderId { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public DateTime ChangedAt { get; set; }

    // Keycloak user id (token "sub") of the buyer or the admin who made the change.
    [StringLength(50)]
    public string ChangedBy { get; set; } = null!;

    [ForeignKey("OrderId")]
    [InverseProperty("StatusChanges")]
    public virtual Order Order { get; set; } = null!;
}
