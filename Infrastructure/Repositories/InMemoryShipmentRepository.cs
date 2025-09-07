using System;
using System.Collections.Generic;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Infrastructure.Repositories
{
    public sealed class InMemoryShipmentRepository : IShipmentRepository
    {
        private readonly Dictionary<string, Shipment> _shipments = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryShipmentRepository()
        {
            // Seed a couple of shipments for testing
            var s1 = new Shipment("SHP-1001", "ManuCo", "DistCo");
            s1.AddPackToken("ABCD-1234-XYZ");
            s1.AddPackToken("BATCH-2025-08-0001");

            var s2 = new Shipment("SHP-1002", "DistCo", "RetailCo");

            Upsert(s1);
            Upsert(s2);
        }

        public Shipment? FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _shipments.TryGetValue(id.Trim(), out var s) ? s : null;
        }

        public void Upsert(Shipment shipment)
        {
            if (shipment is null) throw new ArgumentNullException(nameof(shipment));
            _shipments[shipment.Id] = shipment;
        }

        public IEnumerable<Shipment> List(int skip = 0, int take = 100)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 100;
            int i = 0, yielded = 0;
            foreach (var kv in _shipments)
            {
                if (i++ < skip) continue;
                yield return kv.Value;
                if (++yielded >= take) yield break;
            }
        }
    }
}
