using System;
using System.Collections.Generic;

namespace PharmaChainLite.Domain
{
    public sealed class Shipment
    {
        public string Id { get; }
        public string FromParty { get; }
        public string ToParty { get; }
        public ShipmentStatus Status { get; private set; }
        public DateTime CreatedAt { get; }
        public DateTime? DeliveredAt { get; private set; }

        // For simplicity we record Pack tokens included in this shipment
        private readonly List<string> _packTokens = new();
        public IReadOnlyList<string> PackTokens => _packTokens.AsReadOnly();

        public Shipment(string id, string fromParty, string toParty)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(fromParty)) throw new ArgumentException("From party is required.", nameof(fromParty));
            if (string.IsNullOrWhiteSpace(toParty)) throw new ArgumentException("To party is required.", nameof(toParty));

            Id = id.Trim();
            FromParty = fromParty.Trim();
            ToParty = toParty.Trim();
            Status = ShipmentStatus.Packed;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddPackToken(string token)
        {
            if (Status != ShipmentStatus.Packed)
                throw new InvalidOperationException("Can only add packs while shipment is Packed.");

            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token is required.", nameof(token));

            token = token.Trim();
            if (!_packTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
                _packTokens.Add(token);
        }

        public void RemovePackToken(string token)
        {
            if (Status != ShipmentStatus.Packed)
                throw new InvalidOperationException("Can only remove packs while shipment is Packed.");

            if (string.IsNullOrWhiteSpace(token)) return;
            _packTokens.RemoveAll(t => string.Equals(t, token.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public void TransitionTo(ShipmentStatus next)
        {
            if (!IsValidTransition(Status, next))
                throw new InvalidOperationException($"Illegal transition {Status} → {next}.");

            Status = next;
            if (next == ShipmentStatus.Delivered)
                DeliveredAt = DateTime.UtcNow;
        }

        private static bool IsValidTransition(ShipmentStatus current, ShipmentStatus next)
            => (current, next) switch
            {
                (ShipmentStatus.Packed,    ShipmentStatus.InTransit) => true,
                (ShipmentStatus.InTransit, ShipmentStatus.Delivered) => true,
                _ => false
            };
    }
}
