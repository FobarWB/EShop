using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EShop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Edited by hand: the generated AlterColumn has no USING, and PostgreSQL would read the old values
            // in the session's time zone. The API stored UTC there, so say so explicitly.
            migrationBuilder.Sql(
                """
                ALTER TABLE orders
                    ALTER COLUMN shipped_date TYPE timestamp with time zone USING shipped_date AT TIME ZONE 'UTC',
                    ALTER COLUMN required_date TYPE timestamp with time zone USING required_date AT TIME ZONE 'UTC',
                    ALTER COLUMN order_date TYPE timestamp with time zone USING order_date AT TIME ZONE 'UTC';
                """);

            // Edited by hand: the default fills the existing rows (the Northwind rows are finished orders) and is dropped
            // right after, so a new order never becomes Delivered by omission. The generated '' fails ck_orders_status.
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Delivered");

            migrationBuilder.Sql("ALTER TABLE orders ALTER COLUMN status DROP DEFAULT;");

            // Orders placed through the API (user_id set) before this migration are still open.
            migrationBuilder.Sql("UPDATE orders SET status = 'Pending' WHERE user_id IS NOT NULL;");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "product_name",
                table: "order_details",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            // Edited by hand: existing order lines get the current product names (the best snapshot available).
            migrationBuilder.Sql(
                """
                UPDATE order_details d
                SET product_name = p.product_name
                FROM products p
                WHERE p.product_id = d.product_id;

                ALTER TABLE order_details ALTER COLUMN product_name DROP DEFAULT;
                """);

            migrationBuilder.CreateTable(
                name: "order_status_changes",
                columns: table => new
                {
                    order_status_change_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_status_changes", x => x.order_status_change_id);
                    table.CheckConstraint("ck_order_status_changes_from_status", "from_status IN ('Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled')");
                    table.CheckConstraint("ck_order_status_changes_to_status", "to_status IN ('Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_order_status_changes_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_user_id_order_date",
                table: "orders",
                columns: new[] { "user_id", "order_date" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "status IN ('Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "ix_order_status_changes_order_id_changed_at",
                table: "order_status_changes",
                columns: new[] { "order_id", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_status_changes");

            migrationBuilder.DropIndex(
                name: "ix_orders_user_id_order_date",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "status",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "product_name",
                table: "order_details");

            // The reverse of Up: the UTC wall-clock time, whatever the session's time zone.
            migrationBuilder.Sql(
                """
                ALTER TABLE orders
                    ALTER COLUMN shipped_date TYPE timestamp without time zone USING shipped_date AT TIME ZONE 'UTC',
                    ALTER COLUMN required_date TYPE timestamp without time zone USING required_date AT TIME ZONE 'UTC',
                    ALTER COLUMN order_date TYPE timestamp without time zone USING order_date AT TIME ZONE 'UTC';
                """);
        }
    }
}
