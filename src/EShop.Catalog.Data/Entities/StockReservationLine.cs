namespace EShop.Catalog.Data.Entities;

// No foreign key to products: a product can be deleted while its order lives, and the line is a record.
public class StockReservationLine
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }

    public virtual StockReservation Reservation { get; set; } = null!;
}
