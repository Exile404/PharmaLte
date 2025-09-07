using System;
using PharmaChainLite.Application.Events;
using PharmaChainLite.Application.Payments;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Application.Sales
{
    /// <summary>
    /// Coordinates the retail sale of a Pack:
    ///  1) Guard and set Pack status to Sold.
    ///  2) Publish domain events (PackStatusChanged, PackSold).
    ///  3) Delegate to PaymentService to record the retail ledger entry.
    /// </summary>
    public sealed class SalesService
    {
        private readonly IPackRepository _packs;
        private readonly IEventBus _bus;
        private readonly PaymentService _payments;

        public SalesService(IPackRepository packs, IEventBus bus, PaymentService payments)
        {
            _packs = packs ?? throw new ArgumentNullException(nameof(packs));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        }

        /// <summary>
        /// Marks the pack as Sold (only allowed from Delivered), publishes events,
        /// and records the retail payment (Customer -> Retailer).
        /// </summary>
        public void SellPack(string packToken, string retailer, string customer, decimal? salePrice = null)
        {
            if (string.IsNullOrWhiteSpace(packToken)) throw new ArgumentException("Pack token is required.", nameof(packToken));
            if (string.IsNullOrWhiteSpace(retailer)) throw new ArgumentException("Retailer is required.", nameof(retailer));
            if (string.IsNullOrWhiteSpace(customer)) throw new ArgumentException("Customer is required.", nameof(customer));

            var pack = _packs.FindByToken(packToken.Trim())
                       ?? throw new InvalidOperationException($"Pack '{packToken}' not found.");

            if (pack.Status == PackStatus.Sold)
                throw new InvalidOperationException("Pack is already sold.");

            if (pack.Status != PackStatus.Delivered)
                throw new InvalidOperationException("Only delivered packs can be sold.");

            var before = pack.Status;
            pack.SetStatus(PackStatus.Sold);
            _packs.Upsert(pack);

            // Publish domain events
            _bus.Publish(new PackStatusChanged(pack.Token, before, PackStatus.Sold, DateTime.UtcNow));
            _bus.Publish(new PackSold(pack.Token, DateTime.UtcNow));

            // Record ledger entry via payment strategy/service
            _payments.RecordRetailSale(retailer.Trim(), customer.Trim(), pack.Token, salePrice);
        }
    }
}
