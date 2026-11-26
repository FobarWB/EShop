using EShop.CustomerApi.Data.Entities;
using EShop.Messaging;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.CustomerApi.Data;

// CustomerApi's tables: the shop data of Keycloak users (phone, saved shipping addresses).
public class CustomerDbContext(DbContextOptions<CustomerDbContext> options)
    : DbContext(options),
        IServiceDbContext
{
    public static string Schema => "customer";
    public static string ConnectionStringName => "Customer";

    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            // Two indexes on the same column need names: HasIndex without a name returns the same index.
            // The user's address list.
            entity.HasIndex(e => e.UserId, "ix_customer_addresses_user_id");

            // At most one default address per user; a concurrent second one fails with 23505.
            entity
                .HasIndex(e => e.UserId, "ix_customer_addresses_user_id_default")
                .HasDatabaseName("ix_customer_addresses_user_id_default")
                .IsUnique()
                .HasFilter("is_default");

            entity
                .HasOne(e => e.Profile)
                .WithMany(p => p.Addresses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Publishes customer.guest-account-ready, consumes order.guest-placed.
        modelBuilder.AddOutbox().AddInbox();
    }
}
