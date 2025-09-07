using System;
using PharmaChainLite.Domain;

namespace PharmaChainLite.Application.Events
{

    public interface IDomainEvent
    {
        DateTime OccurredAt { get; }
    }

    public sealed record ShipmentStatusChanged(
        string ShipmentId,
        ShipmentStatus From,
        ShipmentStatus To,
        DateTime OccurredAt
    ) : IDomainEvent;


    public sealed record PackStatusChanged(
        string Token,
        PackStatus From,
        PackStatus To,
        DateTime OccurredAt
    ) : IDomainEvent;
    public sealed record PackSold(
        string Token,
        DateTime OccurredAt
    ) : IDomainEvent;
}
