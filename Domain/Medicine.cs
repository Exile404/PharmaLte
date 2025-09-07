using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PharmaChainLite.Domain
{
    /// <summary>Admin-maintained medicine master.</summary>
    [BsonIgnoreExtraElements] // tolerate stray/legacy fields
    public sealed class Medicine
    {
        // Store as ObjectId in Mongo, expose as string in the POCO.
        // - If DB has ObjectId: driver converts it to string.
        // - If DB has string: still fine.
        // - On new inserts with empty/null Id, driver will generate an ObjectId.
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonRequired] public string Name         { get; set; } = string.Empty;
        [BsonRequired] public string BatchNo      { get; set; } = string.Empty;
        public string Manufacturer               { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ExpiryUtc               { get; set; }

        public Medicine() { }

        public Medicine(string name, string batchNo, DateTime? expiryUtc, string manufacturer)
        {
            Name         = name        ?? string.Empty;
            BatchNo      = batchNo     ?? string.Empty;
            ExpiryUtc    = expiryUtc;
            Manufacturer = manufacturer?? string.Empty;
        }
    }
}
