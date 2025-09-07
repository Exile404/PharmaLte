using System.Collections.Generic;

namespace PharmaChainLite.Domain.Repositories
{
    public interface ILedgerRepository
    {
        void Add(LedgerEntry entry);
        IEnumerable<LedgerEntry> List(int skip = 0, int take = 200);
    }
}
