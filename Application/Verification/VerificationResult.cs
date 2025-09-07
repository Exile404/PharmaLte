using PharmaChainLite.Domain;

namespace PharmaChainLite.Application.Verification
{
    public sealed class VerificationResult
    {
        public bool Found { get; }
        public bool Duplicate { get; }
        public bool Expired { get; }
        public PackStatus? Status { get; }
        public string Message { get; }

        public VerificationResult(bool found, bool duplicate, bool expired, PackStatus? status, string message)
        {
            Found = found;
            Duplicate = duplicate;
            Expired = expired;
            Status = status;
            Message = message;
        }
    }
}
