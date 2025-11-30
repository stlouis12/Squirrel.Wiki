using Squirrel.Wiki.Contracts.Authentication;

namespace Squirrel.Wiki.Core.Security;

/// <summary>
/// Service for coordinating authentication across different strategies
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user using the appropriate strategy
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available authentication strategies
    /// </summary>
    IEnumerable<IAuthenticationStrategy> GetStrategies();

    /// <summary>
    /// Gets a specific authentication strategy by provider type
    /// </summary>
    IAuthenticationStrategy? GetStrategy(AuthenticationProvider provider);
}
