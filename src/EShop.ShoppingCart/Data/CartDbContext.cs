using EShop.DataService;
using EShop.Messaging;
using EShop.Messaging.Consumers;
using EShop.ShoppingCart.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EShop.ShoppingCart.Data;

// ShoppingCart's tables: the carts and its copy of the catalog's products, fed by ProductApi's events.
public class CartDbContext(DbContextOptions<CartDbContext> options)
    : DbContext(options),
        IServiceDbContext
{
    public static string Schema => "cart";

    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartProduct> CartProducts => Set<CartProduct>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Cart>(entity =>
        {
            // One cart per user.
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => new { e.CartId, e.ProductId });

            entity
                .HasOne(d => d.Cart)
                .WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            // The replica is in the same schema now (#77), so an item can reference it. Its rows are never deleted
            // (a deleted product stays as a tombstone), so Restrict only stops an item of an unknown product.
            entity
                .HasOne<CartProduct>()
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // The id comes from ProductApi's events, not from a sequence of this table.
        modelBuilder.Entity<CartProduct>().Property(e => e.ProductId).ValueGeneratedNever();

        // ShoppingCart only consumes events: no outbox.
        modelBuilder.AddInbox();
    }
}
