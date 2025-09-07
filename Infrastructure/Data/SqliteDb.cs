using System;
using Microsoft.Data.Sqlite;

namespace PharmaChainLite.Infrastructure.Data
{

    public sealed class SqliteDb
    {
        public string ConnectionString { get; }

        public SqliteDb(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("DB file path required.", nameof(filePath));
            ConnectionString = new SqliteConnectionStringBuilder { DataSource = filePath }.ToString();
        }

        public void EnsureCreated()
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            var cmd = con.CreateCommand();
            cmd.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Packs(
    Token        TEXT PRIMARY KEY,
    MedicineName TEXT NOT NULL,
    BatchNo      TEXT NOT NULL,
    ExpiryAt     TEXT NOT NULL,    -- ISO 8601
    Status       INTEGER NOT NULL  -- enum PackStatus
);

CREATE TABLE IF NOT EXISTS PackScans(
    Token     TEXT NOT NULL,
    ScannedAt TEXT NOT NULL,
    FOREIGN KEY(Token) REFERENCES Packs(Token) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Shipments(
    Id          TEXT PRIMARY KEY,
    FromParty   TEXT NOT NULL,
    ToParty     TEXT NOT NULL,
    Status      INTEGER NOT NULL,  -- enum ShipmentStatus
    CreatedAt   TEXT NOT NULL,
    DeliveredAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS ShipmentPacks(
    ShipmentId TEXT NOT NULL,
    Token      TEXT NOT NULL,
    UNIQUE(ShipmentId, Token),
    FOREIGN KEY(ShipmentId) REFERENCES Shipments(Id) ON DELETE CASCADE,
    FOREIGN KEY(Token)      REFERENCES Packs(Token)      ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS LedgerEntries(
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    FromParty  TEXT NOT NULL,
    ToParty    TEXT NOT NULL,
    Amount     REAL NOT NULL,
    Memo       TEXT NOT NULL,
    OccurredAt TEXT NOT NULL
);
";
            cmd.ExecuteNonQuery();
        }
    }
}
