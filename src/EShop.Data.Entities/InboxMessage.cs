using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Data.Entities;

// The messages a consumer has processed: RabbitMQ delivers at least once (and the outbox may publish twice),
// so a consumer looks up the MessageId here first and skips a redelivery (idempotent consumer).
// The key includes the consumer: several consumers of one event each process it once.
[PrimaryKey(nameof(Consumer), nameof(MessageId))]
public class InboxMessage
{
    [StringLength(100)]
    public string Consumer { get; set; } = null!;

    // The RabbitMQ MessageId: the id of the producer's outbox row.
    public Guid MessageId { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime ProcessedAt { get; set; }
}
