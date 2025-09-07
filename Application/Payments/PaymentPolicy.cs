using System.Collections.Generic;
using PharmaChainLite.Domain;

namespace PharmaChainLite.Application.Payments
{

    public interface IPaymentPolicy
    {

        IEnumerable<LedgerEntry> GenerateForShipmentDelivery(
            Shipment shipment,
            IEnumerable<Pack> deliveredPacks,
            decimal unitPrice
        );


        IEnumerable<LedgerEntry> GenerateForRetailSale(
            string retailer,
            string customer,
            Pack pack,
            decimal salePrice
        );
    }
}
