using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EShop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxProducer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_occurred_at",
                table: "outbox_messages");

            migrationBuilder.AddColumn<string>(
                name: "producer",
                table: "outbox_messages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                // Every row written before this migration is OrderApi's, the only producer so far.
                defaultValue: "orderapi");

            // The default only fills the existing rows: a new row without a producer must fail, not silently become
            // OrderApi's (the model has no default, so this also keeps the database in line with it).
            migrationBuilder.Sql("ALTER TABLE outbox_messages ALTER COLUMN producer DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_producer_occurred_at",
                table: "outbox_messages",
                columns: new[] { "producer", "occurred_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_guest_customer_email",
                table: "orders",
                column: "customer_email",
                filter: "placed_as_guest AND user_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_producer_occurred_at",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_orders_guest_customer_email",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "producer",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");
        }
    }
}
