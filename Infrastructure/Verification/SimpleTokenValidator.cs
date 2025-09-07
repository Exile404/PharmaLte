using System;
using System.Text.RegularExpressions;

namespace PharmaChainLite.Application.Verification
{

    public sealed class SimpleTokenValidator : ITokenValidator
    {
        private static readonly Regex Allowed =
            new Regex(@"^[A-Z0-9][A-Z0-9\-]{3,63}$", RegexOptions.Compiled);

        public string? Validate(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "Token is required.";

            var t = token.Trim();

            if (t.Length < 4 || t.Length > 64)
                return "Token length must be between 4 and 64 characters.";

            // Uppercase-only policy for simplicity (UI/service may .ToUpperInvariant() first if desired)
            if (!Allowed.IsMatch(t))
                return "Token may only contain A–Z, 0–9, and '-' (dash), and must start with a letter or digit.";

            return null; // valid
        }
    }
}
