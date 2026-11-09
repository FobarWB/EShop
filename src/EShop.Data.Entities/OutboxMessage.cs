using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EShop.Data.Entities;

public class OutboxMessage
{
    [Key]
    public Guid Id { get; set; }

    // The service that wrote the row; its OutboxPublisher publishes only these. The table is shared by all producers
    // (one database for all services), and one publisher per producer keeps that producer's events in order.
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
