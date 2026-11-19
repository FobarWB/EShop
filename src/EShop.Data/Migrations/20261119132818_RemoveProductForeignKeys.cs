using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cart_items_products_product_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "fk_order_details_products_product_id",
                table: "order_details");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_cart_items_products_product_id",
                table: "cart_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "product_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_order_details_products_product_id",
                table: "order_details",
                column: "product_id",
                principalTable: "products",
                principalColumn: "product_id");
        }
    }
}
