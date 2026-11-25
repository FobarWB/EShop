using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EShop.Messaging.Outbox;

public class OutboxMessage
{
    [Key]
    public Guid Id { get; set; }

    // The service that wrote the row; its OutboxPublisher publishes only these. The column kept the producers apart
    // while they shared one table; since #77 each service has its own, and the column stays until it is removed (#78).
    [StringLength(50)]
    public string Producer { get; set; } = null!;

    [StringLength(100)]
    public string Type { get; set; } = null!;

    [Column(TypeName = "jsonb")]
    public string Payload { get; set; } = null!;

    [Column(TypeName = "timestamp with time zone")]
    public DateTime OccurredAt { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
