using System;
using PharmaChainLite.Application.Events;
using PharmaChainLite.Application.Payments;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;
using PharmaChainLite.Infrastructure.Repositories; // for SqlitePackRepository

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
        /// UI-friendly entry point. Normalizes inputs and performs the sale.
        /// </summary>
        public void RecordSale(string packToken, string retailer, string customer, decimal? salePrice = null)
        {
            var token = Normalize(packToken);
            if (token.Length == 0) throw new ArgumentException("Pack token is required.", nameof(packToken));
            if (string.IsNullOrWhiteSpace(retailer)) throw new ArgumentException("Retailer is required.", nameof(retailer));
            if (string.IsNullOrWhiteSpace(customer)) throw new ArgumentException("Customer is required.", nameof(customer));

            var pack = _packs.FindByToken(token)
                       ?? throw new InvalidOperationException($"Pack '{token}' not found.");

            if (pack.Status == PackStatus.Sold)
                throw new InvalidOperationException("Pack is already sold.");

            if (pack.Status != PackStatus.Delivered)
                throw new InvalidOperationException("Only delivered packs can be sold.");

            var before = pack.Status;

            // Persist status → Sold without relying on pack.Token
            if (_packs is SqlitePackRepository sqlite)
            {
                sqlite.UpsertTokenOnly(token, PackStatus.Sold);
            }
            else
            {
                pack.SetStatus(PackStatus.Sold);
                _packs.Upsert(pack);
            }

            // Publish domain events using the normalized token
            _bus.Publish(new PackStatusChanged(token, before, PackStatus.Sold, DateTime.UtcNow));
            _bus.Publish(new PackSold(token, DateTime.UtcNow));

            // Record ledger entry via payment service
            _payments.RecordRetailSale(retailer.Trim(), customer.Trim(), token, salePrice);
        }

        /// <summary>
        /// Backward-compat shim for existing callers. Forwards to RecordSale.
        /// </summary>
        public void SellPack(string packToken, string retailer, string customer, decimal? salePrice = null)
            => RecordSale(packToken, retailer, customer, salePrice);

        private static string Normalize(string s) => (s ?? string.Empty).Trim().ToUpperInvariant();
    }
}
