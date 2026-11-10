using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "placed_as_guest",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_orders_guest_idempotency_key",
                table: "orders",
                column: "idempotency_key",
                unique: true,
                filter: "placed_as_guest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_guest_idempotency_key",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "placed_as_guest",
                table: "orders");
        }
    }
}
