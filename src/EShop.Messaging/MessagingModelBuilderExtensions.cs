using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EShop.Messaging;

// The tables of the outbox and the inbox, called in OnModelCreating of each service's DbContext: the messaging
// infrastructure owns their shape, the service owns the database they live in.
public static class MessagingModelBuilderExtensions
{
    // For producers: the events their transactions store, published by OutboxPublisher.
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            // The pending rows of one producer, oldest first: the publisher's query.
            entity
                .HasIndex(m => new { m.Producer, m.OccurredAt })
                .HasFilter("processed_at IS NULL");
        });
        return modelBuilder;
    }

    // For consumers: the messages MessageConsumer has processed.
    public static ModelBuilder AddInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>().ToTable("inbox_messages");
        return modelBuilder;
    }
}
