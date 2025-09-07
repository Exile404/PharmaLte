using System;
using System.Collections.Generic;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Infrastructure.Repositories
{
    public sealed class InMemoryPackRepository : IPackRepository
    {
        private readonly Dictionary<string, Pack> _packs = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _scans = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryPackRepository()
        {
            // Seed with a couple of tokens for testing
            Upsert(new Pack("ABCD-1234-XYZ", DateTime.Today.AddMonths(6), PackStatus.InTransit));
            Upsert(new Pack("BATCH-2025-08-0001", DateTime.Today.AddMonths(3), PackStatus.Produced));
        }

        public Pack? FindByToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            return _packs.TryGetValue(token.Trim(), out var p) ? p : null;
        }

        public void Upsert(Pack pack)
        {
            _packs[pack.Token] = pack;
        }

        public bool HasScan(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return _scans.Contains(token.Trim());
        }

        public void RecordScan(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            _scans.Add(token.Trim());
        }
    }
}
