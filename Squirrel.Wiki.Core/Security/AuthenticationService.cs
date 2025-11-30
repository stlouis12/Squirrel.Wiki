using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Authentication;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Service implementation for coordinating authentication across different strategies
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IEnumerable<IAuthenticationStrategy> _strategies;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IEnumerable<IAuthenticationStrategy> strategies,
        ILogger<AuthenticationService> logger)
    {
        _strategies = strategies;
        _logger = logger;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        // Find the appropriate strategy for this request
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(request));

        if (strategy == null)
        {
            _logger.LogWarning("No authentication strategy found for provider {Provider}", request.Provider);
            return AuthenticationResult.Failed(
                "No authentication strategy available for this request",
                AuthenticationFailureReason.InvalidCredentials);
        }

        _logger.LogDebug("Using {StrategyType} for authentication", strategy.GetType().Name);

        try
        {
            return await strategy.AuthenticateAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed with exception for provider {Provider}", request.Provider);
            return AuthenticationResult.Failed(
                "An error occurred during authentication",
                AuthenticationFailureReason.ExternalProviderError);
        }
    }

    public IEnumerable<IAuthenticationStrategy> GetStrategies()
    {
        return _strategies;
    }

    public IAuthenticationStrategy? GetStrategy(AuthenticationProvider provider)
    {
        return _strategies.FirstOrDefault(s => s.Provider == provider);
    }
}
