namespace EShop.Data.Entities;

// Stored as text with a CHECK constraint (orders.status): readable in psql, and the database rejects unknown values.
public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Delivered,
    Cancelled,
}
