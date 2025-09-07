using System;
using System.Collections.Generic;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Infrastructure.Repositories
{
    public sealed class InMemoryLedgerRepository : ILedgerRepository
    {
        private readonly List<LedgerEntry> _entries = new();
        private readonly object _gate = new();

        public void Add(LedgerEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            lock (_gate)
            {
                _entries.Add(entry);
            }
        }

        public IEnumerable<LedgerEntry> List(int skip = 0, int take = 200)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 200;

            List<LedgerEntry> snapshot;
            lock (_gate)
            {
                snapshot = new List<LedgerEntry>(_entries);
            }

            for (int i = skip; i < snapshot.Count && take > 0; i++, take--)
                yield return snapshot[i];
        }
    }
}
