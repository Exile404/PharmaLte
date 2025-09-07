using System;
using System.Collections.Generic;
using System.Linq;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;
using PharmaChainLite.Infrastructure.Repositories; // for SqlitePackRepository

namespace PharmaChainLite.Application.Shipments
{
    /// <summary>
    /// Application service coordinating shipment workflows and side effects on Packs.
    /// </summary>
    public sealed class ShipmentService
    {
        private readonly IPackRepository _packs;
        private readonly IShipmentRepository _shipments;

        // flip to true if you want console traces
        private readonly bool _debug = false;

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

            // Normalize once and use this exact value everywhere
            var token = Normalize(packToken);
            if (token.Length == 0)
                throw new ArgumentException("Pack token is required.", nameof(packToken));

            if (shipment.Status != ShipmentStatus.Packed)
                throw new InvalidOperationException("Can only add packs while shipment is Packed.");

            // Try to load the pack
            var pack = _packs.FindByToken(token);
            if (pack == null && _packs is SqlitePackRepository sqlite)
            {
                // create minimal record so later flows (scan/verify) can find it
                sqlite.UpsertTokenOnly(token, PackStatus.Produced);
                pack = _packs.FindByToken(token); // optional read-back
            }

            if (pack == null)
                throw new InvalidOperationException($"Pack '{token}' was not found.");

            if (pack.Status == PackStatus.Sold)
                throw new InvalidOperationException("Cannot add a sold pack to a shipment.");

            // CRITICAL: add the normalized token, not pack.Token (which may be empty via reflection)
            shipment.AddPackToken(token);
            _shipments.Upsert(shipment);

            if (_debug) Console.WriteLine($"[ShipmentService.AddPack] {token} -> {shipment.Id}");
            return shipment;
        }

        public Shipment RemovePack(string shipmentId, string packToken)
        {
            var shipment = RequireShipment(shipmentId);

            if (shipment.Status != ShipmentStatus.Packed)
                throw new InvalidOperationException("Can only remove packs while shipment is Packed.");

            var token = Normalize(packToken);
            if (token.Length == 0) throw new ArgumentException("Pack token is required.", nameof(packToken));

            shipment.RemovePackToken(token);
            _shipments.Upsert(shipment);

            if (_debug) Console.WriteLine($"[ShipmentService.RemovePack] {token} x {shipment.Id}");
            return shipment;
        }

        public Shipment Transition(string shipmentId, ShipmentStatus nextStatus)
        {
            var shipment = RequireShipment(shipmentId);
            var before = shipment.Status;

            // Sanitize token list BEFORE and AFTER transition, just in case old data had blanks
            var tokensBefore = SanitizeTokens(shipment.PackTokens);

            shipment.TransitionTo(nextStatus);
            _shipments.Upsert(shipment); // persist state change first

            var tokensAfter = SanitizeTokens(shipment.PackTokens);

            // Side effects on packs based on shipment status
            if (before != nextStatus)
            {
                switch (nextStatus)
                {
                    case ShipmentStatus.InTransit:
                        SetPacksStatus(tokensAfter, PackStatus.InTransit);
                        break;
                    case ShipmentStatus.Delivered:
                        SetPacksStatus(tokensAfter, PackStatus.Delivered);
                        break;
                }
            }

            if (_debug) Console.WriteLine($"[ShipmentService.Transition] {shipment.Id}: {before} -> {nextStatus} on {tokensAfter.Count} tokens");
            return shipment;
        }

        public IEnumerable<Shipment> List(int skip = 0, int take = 100) => _shipments.List(skip, take);

        // ---------------------------------------------------------------------
        // internals
        // ---------------------------------------------------------------------

        private Shipment RequireShipment(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Shipment id is required.", nameof(id));
            return _shipments.FindById(id.Trim())
                   ?? throw new InvalidOperationException($"Shipment '{id}' was not found.");
        }

        /// <summary>
        /// Strong, repo-optimized pack status update. Avoids reading pack.Token entirely.
        /// </summary>
        private void SetPacksStatus(IEnumerable<string> tokens, PackStatus status)
        {
            if (_packs is SqlitePackRepository sqlite)
            {
                foreach (var t in tokens)
                {
                    var token = Normalize(t);
                    if (token.Length == 0) { if (_debug) Console.WriteLine("[SetPacksStatus] skip blank"); continue; }

                    // direct write by token avoids reflection/constructor pitfalls
                    sqlite.UpsertTokenOnly(token, status);
                    if (_debug) Console.WriteLine($"[SetPacksStatus] {token} -> {status}");
                }
            }
            else
            {
                // Fallback for other repo implementations
                foreach (var t in tokens)
                {
                    var token = Normalize(t);
                    if (token.Length == 0) continue;

                    var pack = _packs.FindByToken(token);
                    if (pack == null) continue;

                    pack.SetStatus(status);
                    _packs.Upsert(pack);
                }
            }
        }

        private static string Normalize(string s) => (s ?? string.Empty).Trim().ToUpperInvariant();

        private static List<string> SanitizeTokens(IEnumerable<string> tokens) =>
            (tokens ?? Enumerable.Empty<string>())
            .Select(Normalize)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
