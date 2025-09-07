using PharmaChainLite.Domain;

namespace PharmaChainLite.Domain.Repositories
{
    public interface IPackRepository
    {
        Pack? FindByToken(string token);
        void Upsert(Pack pack);

        // Simple scan ledger for duplicate-detection
        bool HasScan(string token);
        void RecordScan(string token);
    }
}
