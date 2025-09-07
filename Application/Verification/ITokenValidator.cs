namespace PharmaChainLite.Application.Verification
{

    public interface ITokenValidator
    {
        string? Validate(string token);
    }
}
