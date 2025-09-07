using System;
using System.Collections.Generic;
using PharmaChainLite.Domain;

namespace PharmaChainLite.Application.Payments
{
    /// <summary>
    /// Simple Strategy: generate one ledger entry per pack using a fixed unit price.
    /// ToParty owes FromParty on delivery; Customer owes Retailer on retail sale.
    /// </summary>
    public sealed class PerUnitPaymentPolicy : IPaymentPolicy
    {
        private readonly Func<DateTime> _clock;

        public PerUnitPaymentPolicy(Func<DateTime>? clock = null)
        {
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public IEnumerable<LedgerEntry> GenerateForShipmentDelivery(
            Shipment shipment,
            IEnumerable<Pack> deliveredPacks,
            decimal unitPrice
        )
        {
            if (shipment is null) throw new ArgumentNullException(nameof(shipment));
            if (deliveredPacks is null) yield break;

            var ts = _clock();

            foreach (var pack in deliveredPacks)
            {
                // ToParty (receiver) owes FromParty (sender) per delivered pack
                yield return new LedgerEntry(
                    from: shipment.ToParty,
                    to: shipment.FromParty,
                    amount: unitPrice,
                    memo: $"Delivery of pack {pack.Token} on shipment {shipment.Id}",
                    occurredAt: ts
                );
            }
        }

        public IEnumerable<LedgerEntry> GenerateForRetailSale(
            string retailer,
            string customer,
            Pack pack,
            decimal salePrice
        )
        {
            if (pack is null) yield break;

            yield return new LedgerEntry(
                from: customer,
                to: retailer,
                amount: salePrice,
                memo: $"Retail sale of pack {pack.Token}",
                occurredAt: _clock()
            );
        }
    }
}
