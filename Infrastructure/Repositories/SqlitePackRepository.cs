using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;           // <— for GetUninitializedObject
using Microsoft.Data.Sqlite;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;
using PharmaChainLite.Infrastructure.Data;

namespace PharmaChainLite.Infrastructure.Repositories
{
    /// <summary>
    /// SQLite-backed Pack repository that only assumes:
    ///  - Pack has Token (string),
    ///  - Pack may expose Status (enum) and/or SetStatus(PackStatus) (optional).
    /// It does NOT require medicine/batch/expiry properties or specific constructors.
    /// </summary>
    public sealed class SqlitePackRepository : IPackRepository
    {
        private readonly SqliteDb _db;

        public SqlitePackRepository(SqliteDb db) => _db = db ?? throw new ArgumentNullException(nameof(db));

        public Pack? FindByToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Token, Status FROM Packs WHERE Token = $t LIMIT 1";
            cmd.Parameters.AddWithValue("$t", token.Trim().ToUpperInvariant());

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            var t = r.GetString(0);
            var st = (PackStatus)r.GetInt32(1);

            // Robust construction: try normal ctors; if none work, create an uninitialized instance and set fields.
            var pack = CreatePackInstance(t, st);
            return pack;
        }

        public void Upsert(Pack pack)
        {
            if (pack is null) throw new ArgumentNullException(nameof(pack));
            UpsertTokenOnly(GetToken(pack), GetStatus(pack));
        }

        /// <summary>
        /// Token-only upsert to support domains where Pack doesn't expose a usable constructor.
        /// Works with either minimal schema (Token, Status) or extended schema (adds placeholders).
        /// </summary>
        // inside class SqlitePackRepository ...

        public void UpsertTokenOnly(string token, PackStatus status = PackStatus.Produced)
        {
            token = (token ?? string.Empty).Trim().ToUpperInvariant();   // <-- trim first
            if (token.Length == 0) throw new ArgumentException("Token is required.", nameof(token));

            using var con = new Microsoft.Data.Sqlite.SqliteConnection(_db.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Packs(Token, MedicineName, BatchNo, ExpiryAt, Status)
VALUES($t,$m,$b,$e,$s)
ON CONFLICT(Token) DO UPDATE SET
    MedicineName = excluded.MedicineName,
    BatchNo      = excluded.BatchNo,
    ExpiryAt     = excluded.ExpiryAt,
    Status       = excluded.Status;";
            cmd.Parameters.AddWithValue("$t", token);
            cmd.Parameters.AddWithValue("$m", "");
            cmd.Parameters.AddWithValue("$b", "");
            cmd.Parameters.AddWithValue("$e", new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc).ToString("o"));
            cmd.Parameters.AddWithValue("$s", (int)status);

            try { cmd.ExecuteNonQuery(); }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                using var cmd2 = con.CreateCommand();
                cmd2.CommandText = @"
INSERT INTO Packs(Token, Status)
VALUES($t,$s)
ON CONFLICT(Token) DO UPDATE SET
    Status = excluded.Status;";
                cmd2.Parameters.AddWithValue("$t", token);
                cmd2.Parameters.AddWithValue("$s", (int)status);
                cmd2.ExecuteNonQuery();
            }
        }


        public IEnumerable<Pack> List(int skip = 0, int take = 100)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 100;

            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
SELECT Token, Status
FROM Packs
ORDER BY Token
LIMIT $take OFFSET $skip";
            cmd.Parameters.AddWithValue("$take", take);
            cmd.Parameters.AddWithValue("$skip", skip);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var t = r.GetString(0);
                var st = (PackStatus)r.GetInt32(1);

                yield return CreatePackInstance(t, st);
            }
        }

        public bool HasScan(string token)
        {
            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM PackScans WHERE Token = $t LIMIT 1";
            cmd.Parameters.AddWithValue("$t", token.Trim().ToUpperInvariant());
            using var r = cmd.ExecuteReader();
            return r.Read();
        }

        public void RecordScan(string token)
        {
            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO PackScans(Token, ScannedAt) VALUES($t, $at)";
            cmd.Parameters.AddWithValue("$t", token.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        // ---- helpers ---------------------------------------------------------

        private static string FarFutureIso() =>
            new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);

        private static string GetToken(Pack p)
        {
            var prop = p.GetType().GetProperty("Token", BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null && prop.PropertyType == typeof(string))
                return (string)prop.GetValue(p)!;
            throw new InvalidOperationException("Pack.Token property is required.");
        }

        private static PackStatus GetStatus(Pack p)
        {
            var prop = p.GetType().GetProperty("Status", BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null && prop.PropertyType == typeof(PackStatus))
                return (PackStatus)prop.GetValue(p)!;
            return PackStatus.Produced;
        }

        private static void SetStatusIfPossible(Pack p, PackStatus st)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var m = p.GetType().GetMethod("SetStatus", flags, binder: null, types: new[] { typeof(PackStatus) }, modifiers: null);
            if (m != null)
            {
                m.Invoke(p, new object[] { st });
                return;
            }

            // If there's no method, try writable property or field
            var statusProp = p.GetType().GetProperty("Status", flags);
            if (statusProp != null && statusProp.CanWrite && statusProp.PropertyType == typeof(PackStatus))
            {
                statusProp.SetValue(p, st);
                return;
            }
            var statusField = p.GetType().GetField("_status", flags) ?? p.GetType().GetField("status", flags);
            if (statusField != null && statusField.FieldType == typeof(PackStatus))
            {
                statusField.SetValue(p, st);
            }
        }

        /// <summary>
        /// Create a Pack instance with best-effort reflection:
        ///  1) Try (string) or parameterless constructor.
        ///  2) If none exist, create an uninitialized object and set Token/Status via reflection.
        /// This NEVER throws — it always returns a Pack instance.
        /// </summary>
        private static Pack CreatePackInstance(string token, PackStatus status)
        {
            var t = typeof(Pack);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // Try (string token)
            var ctor1 = t.GetConstructor(flags, binder: null, types: new[] { typeof(string) }, modifiers: null);
            if (ctor1 != null)
            {
                var p = (Pack)ctor1.Invoke(new object[] { token });
                SetStatusIfPossible(p, status);
                return p;
            }

            // Try parameterless
            var ctor0 = t.GetConstructor(flags, binder: null, types: Type.EmptyTypes, modifiers: null);
            if (ctor0 != null)
            {
                var p = (Pack)ctor0.Invoke(null);
                // Set Token if possible
                var tokenProp = t.GetProperty("Token", flags);
                if (tokenProp != null && tokenProp.CanWrite) tokenProp.SetValue(p, token);
                else
                {
                    var tokenField = t.GetField("_token", flags) ?? t.GetField("token", flags);
                    if (tokenField != null && tokenField.FieldType == typeof(string))
                        tokenField.SetValue(p, token);
                }
                SetStatusIfPossible(p, status);
                return p;
            }

            // Final fallback: allocate without running any constructor, then set fields/properties.
            var obj = (Pack)FormatterServices.GetUninitializedObject(t);

            // Token
            {
                var tokenProp = t.GetProperty("Token", flags);
                if (tokenProp != null && tokenProp.CanWrite) tokenProp.SetValue(obj, token);
                else
                {
                    var tokenField = t.GetField("_token", flags) ?? t.GetField("token", flags);
                    if (tokenField != null && tokenField.FieldType == typeof(string))
                        tokenField.SetValue(obj, token);
                }
            }

            // Status
            SetStatusIfPossible(obj, status);

            return obj;
        }
    }
}
