using EShop.Catalog.Data.Entities;
using EShop.Messaging;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.Catalog.Data;

// The catalog's tables, shared by ProductApi and CategoryApi: one bounded context, two deployables
// (see DatabasePerService).
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options),
        IServiceDbContext
{
    public static string Schema => "catalog";

    // 'Reserved', 'Rejected', ...: the CHECK constraint follows the enum, so a new status shows up in the next migration.
    private static readonly string StockReservationStatusValues = string.Join(
        ", ",
        Enum.GetNames<StockReservationStatus>().Select(name => $"'{name}'")
    );

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<StockReservationLine> StockReservationLines => Set<StockReservationLine>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.ReorderLevel).HasDefaultValue((short)0);
            entity.Property(e => e.UnitPrice).HasDefaultValue(0m);
            entity.Property(e => e.UnitsInStock).HasDefaultValue((short)0);
            entity.Property(e => e.UnitsOnOrder).HasDefaultValue((short)0);

            entity
                .HasOne(d => d.Category)
                .WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId);

            entity
                .HasOne(d => d.Supplier)
                .WithMany(p => p.Products)
                .HasForeignKey(d => d.SupplierId);
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            // The order's id, from OrderApi's events.
            entity.Property(e => e.OrderId).ValueGeneratedNever();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.ToTable(t =>
                t.HasCheckConstraint(
                    "ck_stock_reservations_status",
                    $"status IN ({StockReservationStatusValues})"
                )
            );
        });

        modelBuilder.Entity<StockReservationLine>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.ProductId });
            entity
                .HasOne(d => d.Reservation)
                .WithMany(p => p.Lines)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.AddOutbox().AddInbox();
    }
}
