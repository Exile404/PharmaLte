using System;
using System.Collections.Generic;
using PharmaChainLite.Application.Events;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Application.Payments
{
    /// <summary>
    /// Coordinates payment-side effects:
    ///  - On Shipment Delivered  -> generate ToParty->FromParty entries (per delivered pack).
    ///  - On Retail Sale         -> generate Customer->Retailer entries (per pack).
    ///
    /// Uses IPaymentPolicy (Strategy) so rules/amounts can change without touching callers.
    /// Subscribes to domain events on the in-process IEventBus (Event Aggregator).
    /// </summary>
    public sealed class PaymentService : IDisposable
    {
        private readonly IEventBus _bus;
        private readonly IDisposable _shipmentSub;
        private readonly IPaymentPolicy _policy;
        private readonly ILedgerRepository _ledger;
        private readonly IShipmentRepository _shipments;
        private readonly IPackRepository _packs;

        private readonly decimal _deliveryUnitPrice;
        private readonly decimal _defaultRetailPrice;

        public PaymentService(
            IEventBus bus,
            IPaymentPolicy policy,
            ILedgerRepository ledger,
            IShipmentRepository shipments,
            IPackRepository packs,
            decimal deliveryUnitPrice = 8.50m,
            decimal defaultRetailPrice = 12.00m)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _shipments = shipments ?? throw new ArgumentNullException(nameof(shipments));
            _packs = packs ?? throw new ArgumentNullException(nameof(packs));
            _deliveryUnitPrice = deliveryUnitPrice;
            _defaultRetailPrice = defaultRetailPrice;

            // Subscribe to shipment status changes; generate entries only when it becomes Delivered.
            _shipmentSub = _bus.Subscribe<ShipmentStatusChanged>(OnShipmentStatusChanged);
        }

        /// <summary>
        /// Explicit API for retail sales. SalesService should call this after marking a Pack as Sold.
        /// </summary>
        public void RecordRetailSale(string retailer, string customer, string packToken, decimal? salePrice = null)
        {
            if (string.IsNullOrWhiteSpace(retailer)) throw new ArgumentException("Retailer is required.", nameof(retailer));
            if (string.IsNullOrWhiteSpace(customer)) throw new ArgumentException("Customer is required.", nameof(customer));
            if (string.IsNullOrWhiteSpace(packToken)) throw new ArgumentException("Pack token is required.", nameof(packToken));

            var pack = _packs.FindByToken(packToken.Trim());
            if (pack is null) throw new InvalidOperationException($"Pack '{packToken}' not found.");

            var entries = _policy.GenerateForRetailSale(retailer.Trim(), customer.Trim(), pack, salePrice ?? _defaultRetailPrice);
            AddAll(entries);
        }

        private void OnShipmentStatusChanged(ShipmentStatusChanged e)
        {
            try
            {
                if (e.To != ShipmentStatus.Delivered) return;

                var s = _shipments.FindById(e.ShipmentId);
                if (s is null) return;

                var deliveredPacks = new List<Pack>();
                foreach (var token in s.PackTokens)
                {
                    var p = _packs.FindByToken(token);
                    if (p != null && p.Status == PackStatus.Delivered)
                        deliveredPacks.Add(p);
                }

                var entries = _policy.GenerateForShipmentDelivery(s, deliveredPacks, _deliveryUnitPrice);
                AddAll(entries);
            }
            catch
            {
                // In a production app we would log this.
            }
        }

        private void AddAll(IEnumerable<LedgerEntry> entries)
        {
            foreach (var le in entries)
                _ledger.Add(le);
        }

        public void Dispose()
        {
            _shipmentSub?.Dispose();
        }
    }
}
