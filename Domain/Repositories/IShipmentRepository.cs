using System.Collections.Generic;

namespace PharmaChainLite.Domain.Repositories
{
    public interface IShipmentRepository
    {
        Shipment? FindById(string id);
        void Upsert(Shipment shipment);
        IEnumerable<Shipment> List(int skip = 0, int take = 100);
    }
}
