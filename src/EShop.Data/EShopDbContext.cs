using System;
using System.Collections.Generic;
using EShop.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EShop.Data
{
    public partial class EShopDbContext : DbContext
    {
        public EShopDbContext(DbContextOptions<EShopDbContext> options)
            : base(options) { }

        public virtual DbSet<Category> Categories { get; set; } = null!;
        public virtual DbSet<Customer> Customers { get; set; } = null!;
        public virtual DbSet<CustomerDemographic> CustomerDemographics { get; set; } = null!;
        public virtual DbSet<Employee> Employees { get; set; } = null!;
        public virtual DbSet<Order> Orders { get; set; } = null!;
        public virtual DbSet<OrderDetail> OrderDetails { get; set; } = null!;
        public virtual DbSet<Product> Products { get; set; } = null!;
        public virtual DbSet<Region> Regions { get; set; } = null!;
        public virtual DbSet<Shipper> Shippers { get; set; } = null!;
        public virtual DbSet<Supplier> Suppliers { get; set; } = null!;
        public virtual DbSet<Territory> Territories { get; set; } = null!;
        public virtual DbSet<Cart> Carts { get; set; } = null!;
        public virtual DbSet<CartItem> CartItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cart>(entity =>
            {
                entity.HasKey(e => e.CartId);
                entity.Property(e => e.UserId).IsRequired(); // / Binding to a user, e.g. via UserId.
                entity.HasIndex(e => e.UserId).IsUnique(); // Unique indexer
            });

            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasKey(e => new { e.CartId, e.ProductId }); // Compound key

                entity
                    .HasOne(d => d.Cart)
                    .WithMany(p => p.CartItems)
                    .HasForeignKey(d => d.CartId)
                    .OnDelete(DeleteBehavior.Cascade); // Deleting Cart deletes CartItems

                entity
                    .HasOne(d => d.Product)
                    .WithMany(p => p.CartItems)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Restrict); // Deleting a product does not delete CartItems
            });

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

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.Freight).HasDefaultValue(0m);

                // One order per checkout attempt of a user. The index also serves lookups by user_id
                // (its leading column), so user_id needs no index of its own.
                entity.HasIndex(e => new { e.UserId, e.IdempotencyKey }).IsUnique();

                entity
                    .HasOne(d => d.Customer)
                    .WithMany(p => p.Orders)
                    .HasForeignKey(d => d.CustomerId);

                entity
                    .HasOne(d => d.Employee)
                    .WithMany(p => p.Orders)
                    .HasForeignKey(d => d.EmployeeId);

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

                entity
                    .HasOne(d => d.Product)
                    .WithMany(p => p.OrderDetails)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
            });

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

                entity
                    .HasMany(e => e.CartItems)
                    .WithOne(ci => ci.Product)
                    .HasForeignKey(ci => ci.ProductId);
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

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
