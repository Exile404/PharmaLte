using System.Collections.Generic;
using PharmaChainLite.Domain;

namespace PharmaChainLite.Application.Payments
{
    /// <summary>
    /// Strategy interface for generating ledger entries from domain actions.
    /// Different implementations can compute amounts/flows in different ways.
    /// </summary>
    public interface IPaymentPolicy
    {
        /// <summary>
        /// Called when a shipment is marked Delivered.
        /// Typical flow: ToParty owes FromParty for each delivered pack.
        /// </summary>
        IEnumerable<LedgerEntry> GenerateForShipmentDelivery(
            Shipment shipment,
            IEnumerable<Pack> deliveredPacks,
            decimal unitPrice
        );

        /// <summary>
        /// Called when a pack is sold at retail.
        /// Typical flow: Customer owes Retailer for the pack.
        /// </summary>
        IEnumerable<LedgerEntry> GenerateForRetailSale(
            string retailer,
            string customer,
            Pack pack,
            decimal salePrice
        );
    }
}
