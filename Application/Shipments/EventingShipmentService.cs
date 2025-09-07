using System;
using System.Collections.Generic;
using PharmaChainLite.Application.Events;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Application.Shipments
{

    public sealed class EventingShipmentService
    {
        private readonly ShipmentService _inner;
        private readonly IEventBus _bus;
        private readonly IPackRepository _packs;

        public EventingShipmentService(ShipmentService inner, IEventBus bus, IPackRepository packs)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _packs = packs ?? throw new ArgumentNullException(nameof(packs));
        }

        public Shipment CreateShipment(string id, string fromParty, string toParty)
            => _inner.CreateShipment(id, fromParty, toParty);

        public Shipment AddPack(string shipmentId, string packToken)
        {
            // Normalize once here too (belt & braces with the inner service)
            var token = (packToken ?? string.Empty).Trim().ToUpperInvariant();
            return _inner.AddPack(shipmentId, token);
        }

        public Shipment RemovePack(string shipmentId, string packToken)
        {
            var token = (packToken ?? string.Empty).Trim().ToUpperInvariant();
            return _inner.RemovePack(shipmentId, token);
        }

        public IEnumerable<Shipment> List(int skip = 0, int take = 100)
            => _inner.List(skip, take);


        public Shipment Transition(string shipmentId, ShipmentStatus nextStatus)
        {
            if (string.IsNullOrWhiteSpace(shipmentId))
                throw new ArgumentException("Shipment id is required.", nameof(shipmentId));

            var beforeStatuses = new Dictionary<string, PackStatus>(StringComparer.OrdinalIgnoreCase);
            var shipmentBefore = Find(shipmentId);
            foreach (var t in shipmentBefore.PackTokens)
            {
                var p = _packs.FindByToken((t ?? string.Empty).Trim().ToUpperInvariant());
                if (p != null) beforeStatuses[t] = p.Status;
            }

            var from = shipmentBefore.Status;

            // Perform transition (inner updates packs)
            var shipmentAfter = _inner.Transition(shipmentId, nextStatus);

            // Publish shipment-level event
            _bus.Publish(new ShipmentStatusChanged(
                ShipmentId: shipmentAfter.Id,
                From: from,
                To: shipmentAfter.Status,
                OccurredAt: DateTime.UtcNow
            ));

            // Publish pack-level status changes
            foreach (var t in shipmentAfter.PackTokens)
            {
                var norm = (t ?? string.Empty).Trim().ToUpperInvariant();
                if (norm.Length == 0) continue;

                var after = _packs.FindByToken(norm);
                if (after == null) continue;

                if (beforeStatuses.TryGetValue(t, out var prev) && prev != after.Status)
                {
                    _bus.Publish(new PackStatusChanged(
                        Token: after.Token, // if your Pack exposes Token; otherwise use 'norm'
                        From: prev,
                        To: after.Status,
                        OccurredAt: DateTime.UtcNow
                    ));
                }
            }

            return shipmentAfter;
        }

        private Shipment Find(string id)
        {
            foreach (var s in _inner.List(0, 10_000))
            {
                if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            throw new InvalidOperationException($"Shipment '{id}' was not found.");
        }
    }
}
