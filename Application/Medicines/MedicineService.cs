using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Application.Medicines
{
  
    public sealed class MedicineService
    {
        private readonly IMedicineRepository _meds;
        private readonly IShipmentRepository _shipments;

    
        private const string AdminPin = "1234";

        public MedicineService(IMedicineRepository meds, IShipmentRepository shipments)
        {
            _meds = meds ?? throw new ArgumentNullException(nameof(meds));
            _shipments = shipments ?? throw new ArgumentNullException(nameof(shipments));
        }

        // ------------- Queries -------------
        public IEnumerable<Medicine> List(int skip = 0, int take = 100)
        {
            if (take <= 0) take = 100;
            return _meds.List(skip, take);
        }

        public Medicine? FindByBatch(string batchNo)
        {
            if (string.IsNullOrWhiteSpace(batchNo)) return null;
            return _meds.FindByBatch(batchNo.Trim());
        }

   
        public void AddOrUpdate(
            string name,
            string batchNo,
            DateTime? expiryUtc,
            string manufacturer,
            string adminPin,
            string? fromParty = null,
            string? toParty   = null,
            decimal? price    = null)
        {
            EnsurePin(adminPin);

            name         = (name ?? "").Trim();
            batchNo      = (batchNo ?? "").Trim();
            manufacturer = (manufacturer ?? "").Trim();
            fromParty    = string.IsNullOrWhiteSpace(fromParty) ? "ManuCo" : fromParty.Trim();
            toParty      = string.IsNullOrWhiteSpace(toParty)   ? "DistCo" : toParty.Trim();

            if (name.Length == 0)        throw new ArgumentException("Name is required", nameof(name));
            if (batchNo.Length == 0)     throw new ArgumentException("Batch No is required", nameof(batchNo));
            if (manufacturer.Length == 0) manufacturer = "Unknown";

            // Build medicine and (if present) set a Price property via reflection
            var med = new Medicine(name, batchNo, expiryUtc, manufacturer);
            if (price.HasValue) TrySet(med, "Price", price.Value);

            _meds.Upsert(med);

            // Ensure shipment shell exists (Packed, no packs) using provided parties
            var shipmentId = BuildShipmentIdFromBatch(batchNo);
            var existing = _shipments.FindById(shipmentId);
            if (existing == null)
            {
                var shell = CreateShipmentFlexible(
                    shipmentId,
                    from: fromParty!,
                    to:   toParty!,
                    status: ShipmentStatus.Packed,
                    tokens: new List<string>()
                );
                _shipments.Upsert(shell);
            }
        }

        public bool Remove(string batchNo, string adminPin)
        {
            EnsurePin(adminPin);
            batchNo = (batchNo ?? "").Trim();
            if (batchNo.Length == 0) return false;
            return _meds.DeleteByBatch(batchNo);
        }

        // ------------- Helpers -------------
        private static void EnsurePin(string pin)
        {
            if (!string.Equals((pin ?? "").Trim(), AdminPin, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Invalid admin PIN.");
        }

        private static string BuildShipmentIdFromBatch(string batchNo)
        {
            var lettersDigits = new string((batchNo ?? "")
                .Where(char.IsLetterOrDigit).ToArray());
            if (lettersDigits.Length == 0) lettersDigits = "BATCH";
            return $"SHP-{lettersDigits.ToUpperInvariant()}";
        }

        private static Shipment CreateShipmentFlexible(string id, string from, string to, ShipmentStatus status, List<string> tokens)
        {
            var t = typeof(Shipment);

            var ctor = t.GetConstructor(new[] { typeof(string), typeof(string), typeof(string), typeof(ShipmentStatus), typeof(IEnumerable<string>) })
                   ?? t.GetConstructor(new[] { typeof(string), typeof(string), typeof(string), typeof(ShipmentStatus), typeof(List<string>) });
            if (ctor != null) return (Shipment)ctor.Invoke(new object[] { id, from, to, status, tokens });

            ctor = t.GetConstructor(new[] { typeof(string), typeof(string), typeof(string) });
            if (ctor != null)
            {
                var s = (Shipment)ctor.Invoke(new object[] { id, from, to });
                TrySet(s, "Status", status);
                TrySet(s, "PackTokens", tokens);
                return s;
            }

            ctor = t.GetConstructor(new[] { typeof(string) });
            if (ctor != null)
            {
                var s = (Shipment)ctor.Invoke(new object[] { id });
                TrySet(s, "FromParty", from);
                TrySet(s, "ToParty", to);
                TrySet(s, "Status", status);
                TrySet(s, "PackTokens", tokens);
                return s;
            }

            ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                var s = (Shipment)ctor.Invoke(null);
                TrySet(s, "Id", id);
                TrySet(s, "FromParty", from);
                TrySet(s, "ToParty", to);
                TrySet(s, "Status", status);
                TrySet(s, "PackTokens", tokens);
                return s;
            }

            throw new InvalidOperationException("Shipment type does not have a usable constructor.");
        }

        private static bool TrySet(object obj, string name, object? value)
        {
            var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null || !pi.CanWrite) return false;
            try { pi.SetValue(obj, value); return true; } catch { return false; }
        }
    }
}
