using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;
using PharmaChainLite.Infrastructure.Data;

namespace PharmaChainLite.Infrastructure.Repositories
{
    public sealed class SqliteLedgerRepository : ILedgerRepository
    {
        private readonly SqliteDb _db;
        public SqliteLedgerRepository(SqliteDb db) => _db = db ?? throw new ArgumentNullException(nameof(db));

        public void Add(LedgerEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT INTO LedgerEntries(FromParty, ToParty, Amount, Memo, OccurredAt)
VALUES($f,$t,$a,$m,$o)";
            cmd.Parameters.AddWithValue("$f", entry.From);
            cmd.Parameters.AddWithValue("$t", entry.To);
            cmd.Parameters.AddWithValue("$a", entry.Amount);
            cmd.Parameters.AddWithValue("$m", entry.Memo);
            cmd.Parameters.AddWithValue("$o", entry.OccurredAt.ToUniversalTime().ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public IEnumerable<LedgerEntry> List(int skip = 0, int take = 200)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 200;

            using var con = new SqliteConnection(_db.ConnectionString);
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
SELECT FromParty, ToParty, Amount, Memo, OccurredAt
FROM LedgerEntries
ORDER BY Id DESC
LIMIT $take OFFSET $skip";
            cmd.Parameters.AddWithValue("$take", take);
            cmd.Parameters.AddWithValue("$skip", skip);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var from = r.GetString(0);
                var to   = r.GetString(1);
                var amt  = Convert.ToDecimal(r.GetDouble(2));
                var memo = r.GetString(3);
                var at   = DateTime.Parse(r.GetString(4), null, System.Globalization.DateTimeStyles.AdjustToUniversal);

                yield return new LedgerEntry(from, to, amt, memo, at);
            }
        }
    }
}
