using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EShop.OrderApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ordering");

            migrationBuilder.CreateTable(
                name: "customer_demographics",
                schema: "ordering",
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
                schema: "ordering",
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
                schema: "ordering",
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
                        principalSchema: "ordering",
                        principalTable: "employees",
                        principalColumn: "employee_id");
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "ordering",
                columns: table => new
                {
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => new { x.consumer, x.message_id });
                });

            migrationBuilder.CreateTable(
                name: "order_products",
                schema: "ordering",
                columns: table => new
                {
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    product_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    units_in_stock = table.Column<short>(type: "smallint", nullable: false),
                    discontinued = table.Column<bool>(type: "boolean", nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_products", x => x.product_id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                schema: "ordering",
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
                schema: "ordering",
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
                name: "customer_customer_demo",
                schema: "ordering",
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
                        principalSchema: "ordering",
                        principalTable: "customer_demographics",
                        principalColumn: "customer_type_id");
                    table.ForeignKey(
                        name: "fk_customer_customer_demo_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "ordering",
                        principalTable: "customers",
                        principalColumn: "customer_id");
                });

            migrationBuilder.CreateTable(
                name: "territories",
                schema: "ordering",
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
                        principalSchema: "ordering",
                        principalTable: "regions",
                        principalColumn: "region_id");
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "ordering",
                columns: table => new
                {
                    order_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: true),
                    placed_as_guest = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    employee_id = table.Column<int>(type: "integer", nullable: true),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    required_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shipped_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.CheckConstraint("ck_orders_status", "status IN ('Pending', 'Confirmed', 'Paid', 'Shipped', 'Delivered', 'Cancelled', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "ordering",
                        principalTable: "customers",
                        principalColumn: "customer_id");
                    table.ForeignKey(
                        name: "fk_orders_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "ordering",
                        principalTable: "employees",
                        principalColumn: "employee_id");
                    table.ForeignKey(
                        name: "fk_orders_shippers_ship_via",
                        column: x => x.ship_via,
                        principalSchema: "ordering",
                        principalTable: "shippers",
                        principalColumn: "shipper_id");
                });

            migrationBuilder.CreateTable(
                name: "employee_territories",
                schema: "ordering",
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
                        principalSchema: "ordering",
                        principalTable: "employees",
                        principalColumn: "employee_id");
                    table.ForeignKey(
                        name: "fk_employee_territories_territories_territory_id",
                        column: x => x.territory_id,
                        principalSchema: "ordering",
                        principalTable: "territories",
                        principalColumn: "territory_id");
                });

            migrationBuilder.CreateTable(
                name: "order_details",
                schema: "ordering",
                columns: table => new
                {
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    product_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    discount = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_details", x => new { x.order_id, x.product_id });
                    table.ForeignKey(
                        name: "fk_order_details_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "order_id");
                });

            migrationBuilder.CreateTable(
                name: "order_status_changes",
                schema: "ordering",
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
                    table.CheckConstraint("ck_order_status_changes_from_status", "from_status IN ('Pending', 'Confirmed', 'Paid', 'Shipped', 'Delivered', 'Cancelled', 'Rejected')");
                    table.CheckConstraint("ck_order_status_changes_to_status", "to_status IN ('Pending', 'Confirmed', 'Paid', 'Shipped', 'Delivered', 'Cancelled', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_order_status_changes_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_customer_demo_customer_type_id",
                schema: "ordering",
                table: "customer_customer_demo",
                column: "customer_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_city",
                schema: "ordering",
                table: "customers",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_customers_company_name",
                schema: "ordering",
                table: "customers",
                column: "company_name");

            migrationBuilder.CreateIndex(
                name: "ix_customers_postal_code",
                schema: "ordering",
                table: "customers",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_customers_region",
                schema: "ordering",
                table: "customers",
                column: "region");

            migrationBuilder.CreateIndex(
                name: "ix_employee_territories_territory_id",
                schema: "ordering",
                table: "employee_territories",
                column: "territory_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_last_name",
                schema: "ordering",
                table: "employees",
                column: "last_name");

            migrationBuilder.CreateIndex(
                name: "ix_employees_postal_code",
                schema: "ordering",
                table: "employees",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_employees_reports_to",
                schema: "ordering",
                table: "employees",
                column: "reports_to");

            migrationBuilder.CreateIndex(
                name: "ix_order_details_order_id",
                schema: "ordering",
                table: "order_details",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_details_product_id",
                schema: "ordering",
                table: "order_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_status_changes_order_id_changed_at",
                schema: "ordering",
                table: "order_status_changes",
                columns: new[] { "order_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id",
                schema: "ordering",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_employee_id",
                schema: "ordering",
                table: "orders",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_guest_customer_email",
                schema: "ordering",
                table: "orders",
                column: "customer_email",
                filter: "placed_as_guest AND user_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_guest_idempotency_key",
                schema: "ordering",
                table: "orders",
                column: "idempotency_key",
                unique: true,
                filter: "placed_as_guest");

            migrationBuilder.CreateIndex(
                name: "ix_orders_order_date",
                schema: "ordering",
                table: "orders",
                column: "order_date");

            migrationBuilder.CreateIndex(
                name: "ix_orders_ship_postal_code",
                schema: "ordering",
                table: "orders",
                column: "ship_postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_orders_ship_via",
                schema: "ordering",
                table: "orders",
                column: "ship_via");

            migrationBuilder.CreateIndex(
                name: "ix_orders_shipped_date",
                schema: "ordering",
                table: "orders",
                column: "shipped_date");

            migrationBuilder.CreateIndex(
                name: "ix_orders_user_id_idempotency_key",
                schema: "ordering",
                table: "orders",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_user_id_order_date",
                schema: "ordering",
                table: "orders",
                columns: new[] { "user_id", "order_date" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_producer_occurred_at",
                schema: "ordering",
                table: "outbox_messages",
                columns: new[] { "producer", "occurred_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_territories_region_id",
                schema: "ordering",
                table: "territories",
                column: "region_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_customer_demo",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "employee_territories",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_details",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_products",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_status_changes",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "customer_demographics",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "territories",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "regions",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "shippers",
                schema: "ordering");
        }
    }
}
