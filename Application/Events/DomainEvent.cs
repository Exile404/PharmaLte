using System;
using PharmaChainLite.Domain;

namespace PharmaChainLite.Application.Events
{
    /// <summary>
    /// Marker contract for domain/application events.
    /// </summary>
    public interface IDomainEvent
    {
        DateTime OccurredAt { get; }
    }

    /// <summary>
    /// Emitted when a shipment changes status (Packed → InTransit → Delivered).
    /// </summary>
    public sealed record ShipmentStatusChanged(
        string ShipmentId,
        ShipmentStatus From,
        ShipmentStatus To,
        DateTime OccurredAt
    ) : IDomainEvent;

    /// <summary>
    /// Emitted when a pack’s status changes (Produced/InTransit/Delivered/Sold).
    /// </summary>
    public sealed record PackStatusChanged(
        string Token,
        PackStatus From,
        PackStatus To,
        DateTime OccurredAt
    ) : IDomainEvent;

    /// <summary>
    /// Emitted when a pack is sold at retail.
    /// </summary>
    public sealed record PackSold(
        string Token,
        DateTime OccurredAt
    ) : IDomainEvent;
}
