using Squirrel.Wiki.Contracts.Authentication;
using Squirrel.Wiki.Contracts.Plugins;

namespace Squirrel.Wiki.Plugins;

/// <summary>
/// Base class for authentication plugins
/// </summary>
public abstract class AuthenticationPluginBase : PluginBase, IAuthenticationPlugin
{
    /// <inheritdoc/>
    public abstract IAuthenticationStrategy CreateStrategy(Dictionary<string, string> config);

    /// <inheritdoc/>
    public abstract string GetLoginButtonHtml(string returnUrl);

    /// <inheritdoc/>
    public virtual string GetLoginButtonIcon()
    {
        // Default icon - can be overridden by specific plugins
        return "bi bi-box-arrow-in-right";
    }
}
