using System;

namespace PharmaChainLite.Domain
{
    public sealed class LedgerEntry
    {
        public string From { get; }
        public string To { get; }
        public decimal Amount { get; }
        public string Memo { get; }
        public DateTime OccurredAt { get; }

        public LedgerEntry(string from, string to, decimal amount, string memo, DateTime occurredAt)
        {
            if (string.IsNullOrWhiteSpace(from)) throw new ArgumentException("From is required.", nameof(from));
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("To is required.", nameof(to));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

            From = from.Trim();
            To = to.Trim();
            Amount = amount;
            Memo = memo?.Trim() ?? "";
            OccurredAt = occurredAt;
        }

        public override string ToString() => $"{OccurredAt:u} | {From} -> {To} | {Amount:C} | {Memo}";
    }
}
