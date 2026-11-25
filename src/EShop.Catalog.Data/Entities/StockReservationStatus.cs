namespace EShop.Catalog.Data.Entities;

// The catalog's decision on the stock of one order (#76). Every status but Reserved is final, so a redelivered or
// reordered order event changes nothing. Stored as text with a CHECK constraint (stock_reservations.status).
public enum StockReservationStatus
{
    // order.placed: the stock of every line is taken.
    Reserved,

    // order.placed: a line was short, nothing is taken.
    Rejected,

    // order.cancelled after Reserved: the stock is returned.
    Released,

    // order.cancelled before order.placed (that one was on a retry): a tombstone, the late order.placed takes nothing.
    Cancelled,
}
