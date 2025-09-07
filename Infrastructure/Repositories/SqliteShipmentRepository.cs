using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;
using PharmaChainLite.Infrastructure.Data;

namespace PharmaChainLite.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-backed Shipment repository that persists shipments and their pack tokens.
    /// During load, it bypasses invariants (no AddPackToken/TransitionTo).
    /// It also manages CreatedAt/UpdatedAt columns.
    /// </summary>
    public sealed class SqliteShipmentRepository : IShipmentRepository
    {
        private readonly SqliteDb _db;
        private readonly bool _debug = false; // set true to log

        public SqliteShipmentRepository(SqliteDb db) => _db = db ?? throw new ArgumentNullException(nameof(db));

        public Shipment? FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();
            EnsureTables(con);

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT Id, FromParty, ToParty, Status FROM Shipments WHERE Id = $id LIMIT 1";
            cmd.Parameters.AddWithValue("$id", id.Trim());

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            var sid  = r.GetString(0);
            var from = r.GetString(1);
            var to   = r.GetString(2);
            var st   = (ShipmentStatus)r.GetInt32(3);

            var tokens = LoadTokens(con, sid);
            var sh = CreateShipmentInstance(sid, from, to, st, tokens);
            if (_debug) Console.WriteLine($"[ShipRepo.FindById] {sid} tokens={tokens.Count} st={st}");
            return sh;
        }

        public void Upsert(Shipment shipment)
        {
            if (shipment is null) throw new ArgumentNullException(nameof(shipment));
            var sid = (shipment.Id ?? string.Empty).Trim();
            if (sid.Length == 0) throw new ArgumentException("Shipment Id is required.", nameof(shipment));

            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();
            EnsureTables(con); // also ensures CreatedAt/UpdatedAt

            using var tx = con.BeginTransaction();
            var nowIso = DateTime.UtcNow.ToString("o");

            // Upsert shipment row (ALWAYS include CreatedAt/UpdatedAt)
            using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Shipments(Id, FromParty, ToParty, Status, CreatedAt, UpdatedAt)
VALUES($id, $from, $to, $st, $now, $now)
ON CONFLICT(Id) DO UPDATE SET
    FromParty = excluded.FromParty,
    ToParty   = excluded.ToParty,
    Status    = excluded.Status,
    UpdatedAt = excluded.UpdatedAt;";
                cmd.Parameters.AddWithValue("$id", sid);
                cmd.Parameters.AddWithValue("$from", shipment.FromParty ?? "");
                cmd.Parameters.AddWithValue("$to", shipment.ToParty ?? "");
                cmd.Parameters.AddWithValue("$st", (int)shipment.Status);
                cmd.Parameters.AddWithValue("$now", nowIso);
                cmd.ExecuteNonQuery();
            }

            // Reconcile tokens -> ShipmentPacks
            var desired = NormalizeTokens(shipment.PackTokens).ToHashSet(StringComparer.Ordinal);
            var existing = LoadTokens(con, sid).ToHashSet(StringComparer.Ordinal);

            foreach (var tok in existing.Except(desired))
            {
                using var del = con.CreateCommand();
                del.Transaction = tx;
                del.CommandText = "DELETE FROM ShipmentPacks WHERE ShipmentId = $sid AND Token = $t";
                del.Parameters.AddWithValue("$sid", sid);
                del.Parameters.AddWithValue("$t", tok);
                del.ExecuteNonQuery();
                if (_debug) Console.WriteLine($"[ShipRepo.Upsert] DEL {sid}:{tok}");
            }

            foreach (var tok in desired.Except(existing))
            {
                using var ins = con.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT OR IGNORE INTO ShipmentPacks(ShipmentId, Token) VALUES($sid, $t)";
                ins.Parameters.AddWithValue("$sid", sid);
                ins.Parameters.AddWithValue("$t", tok);
                ins.ExecuteNonQuery();
                if (_debug) Console.WriteLine($"[ShipRepo.Upsert] ADD {sid}:{tok}");
            }

            tx.Commit();
        }

        public IEnumerable<Shipment> List(int skip = 0, int take = 100)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 100;

            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();
            EnsureTables(con);

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
SELECT Id, FromParty, ToParty, Status
FROM Shipments
ORDER BY Id
LIMIT $take OFFSET $skip";
            cmd.Parameters.AddWithValue("$take", take);
            cmd.Parameters.AddWithValue("$skip", skip);

            using var r = cmd.ExecuteReader();
            var rows = new List<(string Id, string From, string To, ShipmentStatus St)>();
            while (r.Read())
            {
                rows.Add((r.GetString(0), r.GetString(1), r.GetString(2), (ShipmentStatus)r.GetInt32(3)));
            }

            foreach (var row in rows)
            {
                var tokens = LoadTokens(con, row.Id);
                yield return CreateShipmentInstance(row.Id, row.From, row.To, row.St, tokens);
            }
        }

        // ---------------------------------------------------------------------
        // Schema helpers
        // ---------------------------------------------------------------------

        private void EnsureTables(SqliteConnection con)
        {
            using (var cmd = con.CreateCommand())
            {
                // Create with CreatedAt/UpdatedAt if new
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Shipments(
    Id        TEXT PRIMARY KEY,
    FromParty TEXT NOT NULL,
    ToParty   TEXT NOT NULL,
    Status    INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    UpdatedAt TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
CREATE TABLE IF NOT EXISTS ShipmentPacks(
    ShipmentId TEXT NOT NULL,
    Token      TEXT NOT NULL,
    PRIMARY KEY(ShipmentId, Token),
    FOREIGN KEY(ShipmentId) REFERENCES Shipments(Id) ON DELETE CASCADE
);";
                cmd.ExecuteNonQuery();
            }

            // If table already existed without these columns, add them safely (no function default at ALTER time)
            EnsureColumn(con, "Shipments", "CreatedAt");
            EnsureColumn(con, "Shipments", "UpdatedAt");
        }

        /// <summary>
        /// Safe ALTER path for SQLite: add column with no default, then backfill.
        /// </summary>
        private void EnsureColumn(SqliteConnection con, string table, string column)
        {
            if (ColumnExists(con, table, column)) return;

            using (var add = con.CreateCommand())
            {
                // No NOT NULL, no DEFAULT function here (SQLite restriction)
                add.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} TEXT;";
                add.ExecuteNonQuery();
            }

            using (var backfill = con.CreateCommand())
            {
                backfill.CommandText = $@"
UPDATE {table}
SET {column} = strftime('%Y-%m-%dT%H:%M:%fZ','now')
WHERE {column} IS NULL OR TRIM({column}) = '';";
                backfill.ExecuteNonQuery();
            }

            if (_debug) Console.WriteLine($"[ShipRepo.Schema] Added+backfilled {table}.{column}");
        }

        private bool ColumnExists(SqliteConnection con, string table, string column)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table});";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = r.GetString(1);
                if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // Loading helpers (bypass invariants on rehydrate)
        // ---------------------------------------------------------------------

        private static IEnumerable<string> NormalizeTokens(IEnumerable<string> tokens)
            => (tokens ?? Enumerable.Empty<string>())
               .Select(t => (t ?? string.Empty).Trim().ToUpperInvariant())
               .Where(t => t.Length > 0);

        private static List<string> LoadTokens(SqliteConnection con, string shipmentId)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Token FROM ShipmentPacks WHERE ShipmentId = $sid ORDER BY Token";
            cmd.Parameters.AddWithValue("$sid", shipmentId);

            var list = new List<string>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var tok = (r.IsDBNull(0) ? "" : r.GetString(0))?.Trim().ToUpperInvariant() ?? "";
                if (!string.IsNullOrWhiteSpace(tok))
                    list.Add(tok);
            }
            return list;
        }

        private static Shipment CreateShipmentInstance(string id, string from, string to,
                                                       ShipmentStatus status, IEnumerable<string> tokens)
        {
            var t = typeof(Shipment);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            Shipment s;

            // Try best constructor available
            var c3 = t.GetConstructor(flags, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
            if (c3 != null)
            {
                s = (Shipment)c3.Invoke(new object[] { id, from, to });
            }
            else
            {
                var c1 = t.GetConstructor(flags, null, new[] { typeof(string) }, null);
                if (c1 != null)
                {
                    s = (Shipment)c1.Invoke(new object[] { id });
                    SetIfPossible(s, "FromParty", from);
                    SetIfPossible(s, "ToParty", to);
                }
                else
                {
                    var c0 = t.GetConstructor(flags, null, Type.EmptyTypes, null);
                    if (c0 != null)
                    {
                        s = (Shipment)c0.Invoke(null);
                        SetIfPossible(s, "Id", id);
                        SetIfPossible(s, "FromParty", from);
                        SetIfPossible(s, "ToParty", to);
                    }
                    else
                    {
                        s = (Shipment)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
                        SetIfPossible(s, "Id", id);
                        SetIfPossible(s, "FromParty", from);
                        SetIfPossible(s, "ToParty", to);
                    }
                }
            }

            // Set status via property/field (avoid TransitionTo during load)
            SetStatusDirect(s, status);

            // Append tokens directly into the backing collection (avoid AddPackToken during load)
            AppendTokensDirect(s, NormalizeTokens(tokens), flags);

            return s;
        }

        private static void SetIfPossible(Shipment s, string propName, string value)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var p = s.GetType().GetProperty(propName, flags);
            if (p != null && p.CanWrite && p.PropertyType == typeof(string))
            {
                p.SetValue(s, value);
                return;
            }
            var f = s.GetType().GetField($"_{char.ToLowerInvariant(propName[0])}{propName.Substring(1)}", flags)
                 ?? s.GetType().GetField(propName, flags);
            if (f != null && f.FieldType == typeof(string)) f.SetValue(s, value);
        }

        private static void SetStatusDirect(Shipment s, ShipmentStatus status)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var prop = s.GetType().GetProperty("Status", flags);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(ShipmentStatus))
            {
                prop.SetValue(s, status);
                return;
            }

            var field = s.GetType().GetField("_status", flags) ?? s.GetType().GetField("status", flags);
            if (field != null && field.FieldType == typeof(ShipmentStatus))
            {
                field.SetValue(s, status);
                return;
            }

            // last resort
            var trans = s.GetType().GetMethod("TransitionTo", flags, null, new[] { typeof(ShipmentStatus) }, null);
            if (trans != null) trans.Invoke(s, new object[] { status });
        }

        private static void AppendTokensDirect(Shipment s, IEnumerable<string> tokens, BindingFlags flags)
        {
            var prop = s.GetType().GetProperty("PackTokens", flags);
            if (prop != null && typeof(ICollection<string>).IsAssignableFrom(prop.PropertyType))
            {
                if (prop.GetValue(s) is ICollection<string> coll)
                {
                    foreach (var tok in tokens) coll.Add(tok);
                    return;
                }
            }

            var field = s.GetType().GetField("_packTokens", flags) ?? s.GetType().GetField("PackTokens", flags);
            if (field != null && typeof(ICollection<string>).IsAssignableFrom(field.FieldType))
            {
                if (field.GetValue(s) is ICollection<string> coll)
                {
                    foreach (var tok in tokens) coll.Add(tok);
                    return;
                }
            }

        }
    }
}
