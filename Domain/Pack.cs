using System;

namespace PharmaChainLite.Domain
{
    public sealed class Pack
    {
        public string Token { get; }
        public DateTime Expiry { get; }
        public PackStatus Status { get; private set; }

        public Pack(string token, DateTime expiry, PackStatus status = PackStatus.Produced)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token must be provided.", nameof(token));

            Token = token.Trim();
            Expiry = expiry;
            Status = status;
        }

        public bool IsExpired(DateTime? onDate = null)
        {
            var date = (onDate ?? DateTime.Today).Date;
            return Expiry.Date < date;
        }

 
        public void SetStatus(PackStatus newStatus) => Status = newStatus;
    }
}
