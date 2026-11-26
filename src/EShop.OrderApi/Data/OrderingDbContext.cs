using EShop.Messaging;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.OrderApi.Data.Entities;
using EShop.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.OrderApi.Data;

// OrderApi's tables: the orders with their status journal, its copy of the catalog's products, and the Northwind
// tables only orders reference (customers, employees, shippers and theirs).
public class OrderingDbContext(DbContextOptions<OrderingDbContext> options)
    : DbContext(options),
        IServiceDbContext
{
    public static string Schema => "ordering";
    public static string ConnectionStringName => "Ordering";

    // 'Pending', 'Paid', ...: the CHECK constraints follow the enum, so a new status shows up in the next migration.
    private static readonly string OrderStatusValues = string.Join(
        ", ",
        Enum.GetNames<OrderStatus>().Select(name => $"'{name}'")
    );

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<OrderStatusChange> OrderStatusChanges => Set<OrderStatusChange>();
    public DbSet<OrderProduct> OrderProducts => Set<OrderProduct>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerDemographic> CustomerDemographics => Set<CustomerDemographic>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Territory> Territories => Set<Territory>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Shipper> Shippers => Set<Shipper>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.Freight).HasDefaultValue(0m);

            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.ToTable(t =>
                t.HasCheckConstraint("ck_orders_status", $"status IN ({OrderStatusValues})")
            );

            // One order per checkout attempt of a user. The index also serves lookups by user_id
            // (its leading column), so user_id needs no index of its own.
            entity.HasIndex(e => new { e.UserId, e.IdempotencyKey }).IsUnique();

            // One order per guest checkout attempt. Partial: only guest orders, so it stays valid after the order
            // is attached to the new account (user_id set), and the signed-in orders keep their index above.
            entity
                .HasIndex(e => e.IdempotencyKey, "ix_orders_guest_idempotency_key")
                .HasDatabaseName("ix_orders_guest_idempotency_key")
                .IsUnique()
                .HasFilter("placed_as_guest");

            // The guest orders of an email that have no account yet: attached when CustomerApi reports the
            // account (#45).
            entity
                .HasIndex(e => e.CustomerEmail, "ix_orders_guest_customer_email")
                .HasDatabaseName("ix_orders_guest_customer_email")
                .HasFilter("placed_as_guest AND user_id IS NULL");

            // The user's order list: WHERE user_id = … ORDER BY order_date DESC.
            entity.HasIndex(e => new { e.UserId, e.OrderDate });

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders).HasForeignKey(d => d.CustomerId);

            entity.HasOne(d => d.Employee).WithMany(p => p.Orders).HasForeignKey(d => d.EmployeeId);

            entity
                .HasOne(d => d.ShipViaNavigation)
                .WithMany(p => p.Orders)
                .HasForeignKey(d => d.ShipVia);
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.ProductId });

            entity.Property(e => e.Quantity).HasDefaultValue((short)1);

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            // No foreign key to order_products: Northwind's order lines are loaded before the replica is filled
            // (republish), and a line keeps its own name and price.
        });

        modelBuilder.Entity<OrderStatusChange>(entity =>
        {
            entity.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(20);
            // A NULL from_status (the creation) passes: CHECK rejects only a false result, and NULL IN (...) is NULL.
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_order_status_changes_from_status",
                    $"from_status IN ({OrderStatusValues})"
                );
                t.HasCheckConstraint(
                    "ck_order_status_changes_to_status",
                    $"to_status IN ({OrderStatusValues})"
                );
            });

            entity.HasIndex(e => new { e.OrderId, e.ChangedAt });

            // Restrict: the journal is a record; deleting an order with a history is an error, not a cascade.
            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.StatusChanges)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // The id comes from ProductApi's events, not from a sequence of this table.
        modelBuilder.Entity<OrderProduct>().Property(e => e.ProductId).ValueGeneratedNever();

        modelBuilder.Entity<Customer>(entity =>
        {
            entity
                .HasMany(d => d.CustomerTypes)
                .WithMany(p => p.Customers)
                .UsingEntity<Dictionary<string, object>>(
                    "CustomerCustomerDemo",
                    l =>
                        l.HasOne<CustomerDemographic>()
                            .WithMany()
                            .HasForeignKey("CustomerTypeId")
                            .OnDelete(DeleteBehavior.ClientSetNull),
                    r =>
                        r.HasOne<Customer>()
                            .WithMany()
                            .HasForeignKey("CustomerId")
                            .OnDelete(DeleteBehavior.ClientSetNull),
                    j =>
                    {
                        j.HasKey("CustomerId", "CustomerTypeId");

                        j.IndexerProperty<string>("CustomerId").HasMaxLength(5);

                        j.IndexerProperty<string>("CustomerTypeId").HasMaxLength(10);
                    }
                );
        });

        modelBuilder.Entity<CustomerDemographic>(entity =>
        {
            entity.HasKey(e => e.CustomerTypeId);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity
                .HasOne(d => d.ReportsToNavigation)
                .WithMany(p => p.InverseReportsToNavigation)
                .HasForeignKey(d => d.ReportsTo);

            entity
                .HasMany(d => d.Territories)
                .WithMany(p => p.Employees)
                .UsingEntity<Dictionary<string, object>>(
                    "EmployeeTerritory",
                    l =>
                        l.HasOne<Territory>()
                            .WithMany()
                            .HasForeignKey("TerritoryId")
                            .OnDelete(DeleteBehavior.ClientSetNull),
                    r =>
                        r.HasOne<Employee>()
                            .WithMany()
                            .HasForeignKey("EmployeeId")
                            .OnDelete(DeleteBehavior.ClientSetNull),
                    j =>
                    {
                        j.HasKey("EmployeeId", "TerritoryId");

                        j.ToTable("employee_territories");

                        j.IndexerProperty<int>("EmployeeId");

                        j.IndexerProperty<string>("TerritoryId").HasMaxLength(20);
                    }
                );
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasKey(e => e.RegionId);

            entity.Property(e => e.RegionId).ValueGeneratedNever();
        });

        modelBuilder.Entity<Territory>(entity =>
        {
            entity.HasKey(e => e.TerritoryId);

            entity
                .HasOne(d => d.Region)
                .WithMany(p => p.Territories)
                .HasForeignKey(d => d.RegionId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.AddOutbox().AddInbox();
    }
}
