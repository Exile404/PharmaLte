namespace PharmaChainLite.Application.Verification
{
    /// <summary>
    /// Strategy for validating a token’s basic format before lookup.
    /// Return null if valid; otherwise return a user-friendly error message.
    /// </summary>
    public interface ITokenValidator
    {
        string? Validate(string token);
    }
}
