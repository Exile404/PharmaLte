using System;
using System.Collections.Generic;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Application.Shipments
{
    /// <summary>
    /// Application service coordinating shipment workflows and side effects on Packs.
    /// </summary>
    public sealed class ShipmentService
    {
        private readonly IPackRepository _packs;
        private readonly IShipmentRepository _shipments;

        public ShipmentService(IPackRepository packs, IShipmentRepository shipments)
        {
            _packs = packs ?? throw new ArgumentNullException(nameof(packs));
            _shipments = shipments ?? throw new ArgumentNullException(nameof(shipments));
        }

        public Shipment CreateShipment(string id, string fromParty, string toParty)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (_shipments.FindById(id) != null) throw new InvalidOperationException($"Shipment '{id}' already exists.");

            var shipment = new Shipment(id.Trim(), fromParty, toParty);
            _shipments.Upsert(shipment);
            return shipment;
        }

        public Shipment AddPack(string shipmentId, string packToken)
        {
            var shipment = RequireShipment(shipmentId);

            if (string.IsNullOrWhiteSpace(packToken))
                throw new ArgumentException("Pack token is required.", nameof(packToken));

            var pack = _packs.FindByToken(packToken.Trim())
                       ?? throw new InvalidOperationException($"Pack '{packToken}' was not found.");

            if (shipment.Status != ShipmentStatus.Packed)
                throw new InvalidOperationException("Can only add packs while shipment is Packed.");

            if (pack.Status == PackStatus.Sold)
                throw new InvalidOperationException("Cannot add a sold pack to a shipment.");

            shipment.AddPackToken(pack.Token);
            _shipments.Upsert(shipment);
            return shipment;
        }

        public Shipment RemovePack(string shipmentId, string packToken)
        {
            var shipment = RequireShipment(shipmentId);

            if (shipment.Status != ShipmentStatus.Packed)
                throw new InvalidOperationException("Can only remove packs while shipment is Packed.");

            shipment.RemovePackToken(packToken);
            _shipments.Upsert(shipment);
            return shipment;
        }

        public Shipment Transition(string shipmentId, ShipmentStatus nextStatus)
        {
            var shipment = RequireShipment(shipmentId);

            var before = shipment.Status;
            shipment.TransitionTo(nextStatus);

            // Side effects on packs based on shipment status
            if (before != nextStatus)
            {
                switch (nextStatus)
                {
                    case ShipmentStatus.InTransit:
                        SetPacksStatus(shipment.PackTokens, PackStatus.InTransit);
                        break;

                    case ShipmentStatus.Delivered:
                        SetPacksStatus(shipment.PackTokens, PackStatus.Delivered);
                        break;
                }
            }

            _shipments.Upsert(shipment);
            return shipment;
        }

        public IEnumerable<Shipment> List(int skip = 0, int take = 100) => _shipments.List(skip, take);

        private Shipment RequireShipment(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Shipment id is required.", nameof(id));
            return _shipments.FindById(id.Trim())
                   ?? throw new InvalidOperationException($"Shipment '{id}' was not found.");
        }

        private void SetPacksStatus(IEnumerable<string> tokens, PackStatus status)
        {
            foreach (var token in tokens)
            {
                var pack = _packs.FindByToken(token);
                if (pack == null) continue; // if missing, skip; repo may be out-of-sync in test data
                pack.SetStatus(status);
                _packs.Upsert(pack);
            }
        }
    }
}
