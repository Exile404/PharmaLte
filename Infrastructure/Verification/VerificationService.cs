using System;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Application.Verification
{
    public sealed class VerificationService
    {
        private readonly IPackRepository _packs;
        private readonly ITokenValidator _validator;

        public VerificationService(IPackRepository packs, ITokenValidator? validator = null)
        {
            _packs = packs ?? throw new ArgumentNullException(nameof(packs));
            _validator = validator ?? new SimpleTokenValidator();
        }

        public VerificationResult Verify(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new VerificationResult(false, false, false, null, "Token is required.");

            var t = token.Trim().ToUpperInvariant();

            var err = _validator.Validate(t);
            if (err != null)
                return new VerificationResult(false, false, false, null, err);

            var pack = _packs.FindByToken(t);
            if (pack is null)
                return new VerificationResult(false, false, false, null, "Not found - possible counterfeit.");

            var duplicate = _packs.HasScan(t);
            var expired = pack.IsExpired();

            _packs.RecordScan(t);

            var msg = $"OK - Status: {pack.Status}, Duplicate: {duplicate}, Expired: {expired}";
            return new VerificationResult(true, duplicate, expired, pack.Status, msg);
        }
    }
}
