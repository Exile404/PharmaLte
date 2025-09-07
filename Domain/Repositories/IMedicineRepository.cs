using System.Collections.Generic;

namespace PharmaChainLite.Domain.Repositories
{
    using PharmaChainLite.Domain;


    public interface IMedicineRepository
    {
       
        IEnumerable<Medicine> List(int skip = 0, int take = 100);
       
        Medicine? FindByBatch(string batchNo);
        
        void Upsert(Medicine medicine);

        bool DeleteByBatch(string batchNo);
    }
}
