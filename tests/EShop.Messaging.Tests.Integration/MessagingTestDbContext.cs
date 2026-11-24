using EShop.DataService;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EShop.Messaging.Tests.Integration;

// A service's DbContext reduced to what the messaging needs: the outbox, the inbox and one table a handler changes.
// In a schema of its own like every service's context, so the publisher's schema-qualified SQL is tested too.
public sealed class MessagingTestDbContext(DbContextOptions<MessagingTestDbContext> options)
    : DbContext(options),
        IServiceDbContext
{
    public static string Schema => "messaging";

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<TestCounter> Counters => Set<TestCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddOutbox().AddInbox();
    }
}

public sealed class TestCounter
{
    public int Id { get; set; }
    public int Value { get; set; }
}
