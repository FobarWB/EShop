using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCartProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cart_products",
                columns: table => new
                {
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    product_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    units_in_stock = table.Column<short>(type: "smallint", nullable: false),
                    discontinued = table.Column<bool>(type: "boolean", nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_products", x => x.product_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_products");
        }
    }
}
