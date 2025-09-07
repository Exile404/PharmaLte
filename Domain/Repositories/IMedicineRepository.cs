using System.Collections.Generic;

namespace PharmaChainLite.Domain.Repositories
{
    using PharmaChainLite.Domain;

    /// <summary>
    /// Minimal persistence API the app expects for medicines.
    /// </summary>
    public interface IMedicineRepository
    {
        /// <summary>Page through medicines, newest-first or any natural order.</summary>
        IEnumerable<Medicine> List(int skip = 0, int take = 100);

        /// <summary>Find a single medicine by its batch number (case-insensitive when supported).</summary>
        Medicine? FindByBatch(string batchNo);

        /// <summary>Create or replace a medicine by its batch number.</summary>
        void Upsert(Medicine medicine);

        /// <summary>Delete a medicine by batch number. Returns true if something was removed.</summary>
        bool DeleteByBatch(string batchNo);
    }
}
