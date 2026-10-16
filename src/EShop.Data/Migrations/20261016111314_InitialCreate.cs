using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EShop.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    cart_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carts", x => x.cart_id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    category_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_name = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    picture = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.category_id);
                });

            migrationBuilder.CreateTable(
                name: "customer_demographics",
                columns: table => new
                {
                    customer_type_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    customer_desc = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_demographics", x => x.customer_type_id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    customer_id = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    company_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    contact_title = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    city = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    region = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    country = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    phone = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    fax = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.customer_id);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    employee_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    last_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_name = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    title_of_courtesy = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    birth_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    hire_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    address = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    city = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    region = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    country = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    home_phone = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    extension = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    photo = table.Column<byte[]>(type: "bytea", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    reports_to = table.Column<int>(type: "integer", nullable: true),
                    photo_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.employee_id);
                    table.ForeignKey(
                        name: "fk_employees_employees_reports_to",
                        column: x => x.reports_to,
                        principalTable: "employees",
                        principalColumn: "employee_id");
                });

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    region_id = table.Column<int>(type: "integer", nullable: false),
                    region_description = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.region_id);
                });

            migrationBuilder.CreateTable(
                name: "shippers",
                columns: table => new
                {
                    shipper_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    phone = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shippers", x => x.shipper_id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    supplier_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    contact_title = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    city = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    region = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    country = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    phone = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    fax = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    home_page = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.supplier_id);
                });

            migrationBuilder.CreateTable(
                name: "customer_customer_demo",
                columns: table => new
                {
                    customer_id = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    customer_type_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_customer_demo", x => new { x.customer_id, x.customer_type_id });
                    table.ForeignKey(
                        name: "fk_customer_customer_demo_customer_demographics_customer_type_",
                        column: x => x.customer_type_id,
                        principalTable: "customer_demographics",
                        principalColumn: "customer_type_id");
                    table.ForeignKey(
                        name: "fk_customer_customer_demo_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id");
                });

            migrationBuilder.CreateTable(
                name: "territories",
                columns: table => new
                {
                    territory_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    territory_description = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    region_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_territories", x => x.territory_id);
                    table.ForeignKey(
                        name: "fk_territories_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "region_id");
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    order_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    employee_id = table.Column<int>(type: "integer", nullable: true),
                    order_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    required_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    shipped_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ship_via = table.Column<int>(type: "integer", nullable: true),
                    freight = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true, defaultValue: 0m),
                    ship_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ship_address = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ship_city = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ship_region = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ship_postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ship_country = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.order_id);
                    table.ForeignKey(
                        name: "fk_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id");
                    table.ForeignKey(
                        name: "fk_orders_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "employee_id");
                    table.ForeignKey(
                        name: "fk_orders_shippers_ship_via",
                        column: x => x.ship_via,
                        principalTable: "shippers",
                        principalColumn: "shipper_id");
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    product_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_id = table.Column<int>(type: "integer", nullable: true),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    quantity_per_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true, defaultValue: 0m),
                    units_in_stock = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    units_on_order = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    reorder_level = table.Column<short>(type: "smallint", nullable: true, defaultValue: (short)0),
                    discontinued = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.product_id);
                    table.ForeignKey(
                        name: "fk_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "category_id");
                    table.ForeignKey(
                        name: "fk_products_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "supplier_id");
                });

            migrationBuilder.CreateTable(
                name: "employee_territories",
                columns: table => new
                {
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    territory_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_territories", x => new { x.employee_id, x.territory_id });
                    table.ForeignKey(
                        name: "fk_employee_territories_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "employee_id");
                    table.ForeignKey(
                        name: "fk_employee_territories_territories_territory_id",
                        column: x => x.territory_id,
                        principalTable: "territories",
                        principalColumn: "territory_id");
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    cart_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_items", x => new { x.cart_id, x.product_id });
                    table.ForeignKey(
                        name: "fk_cart_items_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "cart_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cart_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_details",
                columns: table => new
                {
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    quantity = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    discount = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_details", x => new { x.order_id, x.product_id });
                    table.ForeignKey(
                        name: "fk_order_details_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "order_id");
                    table.ForeignKey(
                        name: "fk_order_details_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "product_id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_product_id",
                table: "cart_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_user_id",
                table: "carts",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_category_name",
                table: "categories",
                column: "category_name");

            migrationBuilder.CreateIndex(
                name: "ix_customer_customer_demo_customer_type_id",
                table: "customer_customer_demo",
                column: "customer_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_city",
                table: "customers",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_customers_company_name",
                table: "customers",
                column: "company_name");

            migrationBuilder.CreateIndex(
                name: "ix_customers_postal_code",
                table: "customers",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_customers_region",
                table: "customers",
                column: "region");

            migrationBuilder.CreateIndex(
                name: "ix_employee_territories_territory_id",
                table: "employee_territories",
                column: "territory_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_last_name",
                table: "employees",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "ix_employees_postal_code",
                table: "employees",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_employees_reports_to",
                table: "employees",
                column: "reports_to");

            migrationBuilder.CreateIndex(
                name: "ix_order_details_order_id",
                table: "order_details",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_details_product_id",
                table: "order_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_employee_id",
                table: "orders",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_order_date",
                table: "orders",
                column: "order_date");

            migrationBuilder.CreateIndex(
                name: "ix_orders_ship_postal_code",
                table: "orders",
                column: "ship_postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_orders_ship_via",
                table: "orders",
                column: "ship_via");

            migrationBuilder.CreateIndex(
                name: "ix_orders_shipped_date",
                table: "orders",
                column: "shipped_date");

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_product_name",
                table: "products",
                column: "product_name");

            migrationBuilder.CreateIndex(
                name: "ix_products_supplier_id",
                table: "products",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_company_name",
                table: "suppliers",
                column: "company_name");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_postal_code",
                table: "suppliers",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_territories_region_id",
                table: "territories",
                column: "region_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_items");

            migrationBuilder.DropTable(
                name: "customer_customer_demo");

            migrationBuilder.DropTable(
                name: "employee_territories");

            migrationBuilder.DropTable(
                name: "order_details");

            migrationBuilder.DropTable(
                name: "carts");

            migrationBuilder.DropTable(
                name: "customer_demographics");

            migrationBuilder.DropTable(
                name: "territories");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "shippers");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "suppliers");
        }
    }
}
