using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Authentication;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Plugins;

namespace Squirrel.Wiki.Plugins.Oidc;

/// <summary>
/// OpenID Connect authentication plugin for Squirrel Wiki
/// </summary>
public class OidcAuthenticationPlugin : AuthenticationPluginBase
{
    private IServiceProvider? _services;

    public override PluginMetadata Metadata => new()
    {
        Id = "squirrel.auth.oidc",
        Name = "OpenID Connect Authentication",
        Description = "Provides OpenID Connect / OAuth 2.0 authentication support",
        Author = "Squirrel Wiki Team",
        Version = "1.0.0",
        IsCorePlugin = true,
        RequiresConfiguration = true,
        Configuration = GetConfigurationSchema().ToArray()
    };

    public override IEnumerable<PluginConfigurationItem> GetConfigurationSchema()
    {
        return new List<PluginConfigurationItem>
        {
            new()
            {
                Key = "Authority",
                DisplayName = "OIDC Authority",
                Description = "The URL of the OpenID Connect provider (e.g., https://accounts.google.com)",
                IsRequired = true,
                IsSecret = false,
                ValidationPattern = @"^https?://[^\s]+$",
                ValidationErrorMessage = "Must be a valid URL starting with http:// or https://",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_AUTHORITY"
            },
            new()
            {
                Key = "ClientId",
                DisplayName = "Client ID",
                Description = "The client ID registered with the OIDC provider",
                IsRequired = true,
                IsSecret = false,
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_CLIENTID"
            },
            new()
            {
                Key = "ClientSecret",
                DisplayName = "Client Secret",
                Description = "The client secret for authentication",
                IsRequired = true,
                IsSecret = true,
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_CLIENTSECRET"
            },
            new()
            {
                Key = "Scope",
                DisplayName = "Scope",
                Description = "The OAuth scopes to request (space-separated)",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "openid profile email",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_SCOPE"
            },
            new()
            {
                Key = "UsernameClaim",
                DisplayName = "Username Claim",
                Description = "The claim to use for the username",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "preferred_username",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_USERNAMECLAIM"
            },
            new()
            {
                Key = "EmailClaim",
                DisplayName = "Email Claim",
                Description = "The claim to use for the email address",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "email",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_EMAILCLAIM"
            },
            new()
            {
                Key = "DisplayNameClaim",
                DisplayName = "Display Name Claim",
                Description = "The claim to use for the display name",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "name",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_DISPLAYNAMECLAIM"
            },
            new()
            {
                Key = "GroupsClaim",
                DisplayName = "Groups Claim",
                Description = "The claim that contains group memberships",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "groups",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_GROUPSCLAIM"
            },
            new()
            {
                Key = "AdminGroup",
                DisplayName = "Admin Group",
                Description = "The group name that grants admin privileges",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "squirrel-admins",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_ADMINGROUP"
            },
            new()
            {
                Key = "EditorGroup",
                DisplayName = "Editor Group",
                Description = "The group name that grants editor privileges",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "squirrel-editors",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_EDITORGROUP"
            },
            new()
            {
                Key = "AutoCreateUsers",
                DisplayName = "Auto-Create Users",
                Description = "Automatically create user accounts on first login",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "true",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_AUTOCREATEUSERS"
            },
            new()
            {
                Key = "RequireHttpsMetadata",
                DisplayName = "Require HTTPS Metadata",
                Description = "Require HTTPS for metadata endpoint (disable for development only)",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "true",
                EnvironmentVariableName = "PLUGIN_SQUIRREL_AUTH_OIDC_REQUIREHTTPSMETADATA"
            }
        };
    }

    public override async Task<bool> ValidateConfigurationAsync(Dictionary<string, string> config, CancellationToken cancellationToken = default)
    {
        // Validate required fields
        if (!config.ContainsKey("Authority") || string.IsNullOrWhiteSpace(config["Authority"]))
        {
            return false;
        }

        if (!config.ContainsKey("ClientId") || string.IsNullOrWhiteSpace(config["ClientId"]))
        {
            return false;
        }

        if (!config.ContainsKey("ClientSecret") || string.IsNullOrWhiteSpace(config["ClientSecret"]))
        {
            return false;
        }

        // Validate Authority URL format
        if (!Uri.TryCreate(config["Authority"], UriKind.Absolute, out var authorityUri) ||
            (authorityUri.Scheme != "http" && authorityUri.Scheme != "https"))
        {
            return false;
        }

        return await Task.FromResult(true);
    }

    public override Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        _services = serviceProvider;
        return Task.CompletedTask;
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _services = null;
        return Task.CompletedTask;
    }

    public override IAuthenticationStrategy CreateStrategy(Dictionary<string, string> config)
    {
        // Note: This method is called with a disposed service provider from initialization.
        // The actual service provider will be passed when creating the strategy in the controller.
        // This is a placeholder that should not be used directly.
        throw new InvalidOperationException(
            "CreateStrategy() without service provider is not supported for OIDC plugin. " +
            "Use CreateStrategy(IServiceProvider, Dictionary<string, string>) instead.");
    }

    /// <summary>
    /// Creates an authentication strategy with the current request's service provider
    /// </summary>
    public static IAuthenticationStrategy CreateStrategy(IServiceProvider serviceProvider, Dictionary<string, string> config)
    {
        // Get required services from the current request's service provider
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<OidcAuthenticationStrategy>();

        // Create and return the strategy with configuration
        return new OidcAuthenticationStrategy(serviceProvider, config, logger);
    }

    public override string GetLoginButtonHtml(string returnUrl)
    {
        var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
        return $@"
            <a href=""/Account/OidcLogin?returnUrl={encodedReturnUrl}"" 
               class=""btn btn-outline-primary w-100 mb-2"">
                <i class=""bi bi-shield-lock""></i> Sign in with OpenID Connect
            </a>";
    }

    public override string GetLoginButtonIcon()
    {
        return "bi bi-shield-lock";
    }
}
