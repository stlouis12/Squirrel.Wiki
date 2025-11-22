using Squirrel.Wiki.Contracts.Authentication;

namespace Squirrel.Wiki.Contracts.Plugins;

/// <summary>
/// Interface for authentication provider plugins
/// </summary>
public interface IAuthenticationPlugin : IPlugin
{
    /// <summary>
    /// Create an authentication strategy instance with the given configuration
    /// </summary>
    /// <param name="config">Plugin configuration</param>
    /// <returns>Authentication strategy instance</returns>
    IAuthenticationStrategy CreateStrategy(Dictionary<string, string> config);

    /// <summary>
    /// Get the HTML for the login button to display on the login page
    /// </summary>
    /// <param name="returnUrl">URL to return to after authentication</param>
    /// <returns>HTML string for the login button</returns>
    string GetLoginButtonHtml(string returnUrl);

    /// <summary>
    /// Get the icon for the login button (CSS class or SVG)
    /// </summary>
    /// <returns>Icon identifier</returns>
    string GetLoginButtonIcon();
}
