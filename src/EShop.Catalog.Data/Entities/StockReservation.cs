using System.ComponentModel.DataAnnotations;

namespace EShop.Catalog.Data.Entities;

// ProductApi's decision on the stock of one order (#76), keyed by OrderApi's order id.
public class StockReservation
{
    [Key]
    public int OrderId { get; set; }

    public StockReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    // What was taken, to return exactly that on cancel: OrderCancelled has no lines, and order_details is OrderApi's.
    public virtual ICollection<StockReservationLine> Lines { get; set; } =
        new List<StockReservationLine>();
}
