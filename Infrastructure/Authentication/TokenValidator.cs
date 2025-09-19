namespace csharp_user_management.Infrastructure.Authentication;

public interface ITokenValidator
{
    bool HasConfiguredTokens { get; }
    bool IsValid(string token);
}

public sealed class TokenValidator : ITokenValidator
{
    private readonly HashSet<string> _tokens;

    private TokenValidator(HashSet<string> tokens)
    {
        _tokens = tokens;
    }

    public bool HasConfiguredTokens => _tokens.Count > 0;

    public bool IsValid(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _tokens.Contains(token.Trim());
    }

    public static TokenValidator FromConfiguration(IConfiguration configuration)
    {
        var configuredTokens =
            configuration.GetSection("Authentication:Tokens").Get<string[]>()
            ?? Array.Empty<string>();
        var singleToken = configuration["Authentication:Token"];
        var tokenCandidates = string.IsNullOrWhiteSpace(singleToken)
            ? configuredTokens
            : configuredTokens.Append(singleToken).ToArray();

        var validTokens = tokenCandidates
            .Select(token => token?.Trim())
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token!)
            .ToHashSet(StringComparer.Ordinal);

        return new TokenValidator(validTokens);
    }
}
