using EShop.Messaging;
using EShop.Messaging.Consumers;
using EShop.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShop.NotificationService.Data;

// NotificationService's only table: the inbox of the order events it has handled, so a redelivery sends no
// second email.
public class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options),
        IServiceDbContext
{
    public static string Schema => "notification";

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // NotificationService only consumes events: no outbox.
        modelBuilder.AddInbox();
    }
}
