using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmedAndRejectedOrderStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_status_changes_from_status",
                table: "order_status_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_status_changes_to_status",
                table: "order_status_changes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "status IN ('Pending', 'Confirmed', 'Paid', 'Shipped', 'Delivered', 'Cancelled', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_status_changes_from_status",
                table: "order_status_changes",
                sql: "from_status IN ('Pending', 'Confirmed', 'Paid', 'Shipped', 'Delivered', 'Cancelled', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_status_changes_to_status",
                table: "order_status_changes",
                sql: "to_status IN ('Pending', 'Confirmed', 'Paid', 'Shipped', 'Delivered', 'Cancelled', 'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_status_changes_from_status",
                table: "order_status_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_status_changes_to_status",
                table: "order_status_changes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "status IN ('Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_status_changes_from_status",
                table: "order_status_changes",
                sql: "from_status IN ('Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_status_changes_to_status",
                table: "order_status_changes",
                sql: "to_status IN ('Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled')");
        }
    }
}
