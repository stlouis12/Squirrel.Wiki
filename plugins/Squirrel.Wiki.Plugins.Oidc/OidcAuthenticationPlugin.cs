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
                ValidationPattern = @"^https?://.*",
                ValidationErrorMessage = "Must be a valid URL",
                EnvironmentVariableName = "PLUGIN_OIDC_AUTHORITY"
            },
            new()
            {
                Key = "ClientId",
                DisplayName = "Client ID",
                Description = "The client ID registered with the OIDC provider",
                IsRequired = true,
                IsSecret = false,
                EnvironmentVariableName = "PLUGIN_OIDC_CLIENT_ID"
            },
            new()
            {
                Key = "ClientSecret",
                DisplayName = "Client Secret",
                Description = "The client secret for authentication",
                IsRequired = true,
                IsSecret = true,
                EnvironmentVariableName = "PLUGIN_OIDC_CLIENT_SECRET"
            },
            new()
            {
                Key = "Scope",
                DisplayName = "Scope",
                Description = "The OAuth scopes to request (space-separated)",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "openid profile email",
                EnvironmentVariableName = "PLUGIN_OIDC_SCOPE"
            },
            new()
            {
                Key = "UsernameClaim",
                DisplayName = "Username Claim",
                Description = "The claim to use for the username",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "preferred_username",
                EnvironmentVariableName = "PLUGIN_OIDC_USERNAME_CLAIM"
            },
            new()
            {
                Key = "EmailClaim",
                DisplayName = "Email Claim",
                Description = "The claim to use for the email address",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "email",
                EnvironmentVariableName = "PLUGIN_OIDC_EMAIL_CLAIM"
            },
            new()
            {
                Key = "DisplayNameClaim",
                DisplayName = "Display Name Claim",
                Description = "The claim to use for the display name",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "name",
                EnvironmentVariableName = "PLUGIN_OIDC_DISPLAY_NAME_CLAIM"
            },
            new()
            {
                Key = "GroupsClaim",
                DisplayName = "Groups Claim",
                Description = "The claim that contains group memberships",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "groups",
                EnvironmentVariableName = "PLUGIN_OIDC_GROUPS_CLAIM"
            },
            new()
            {
                Key = "AdminGroup",
                DisplayName = "Admin Group",
                Description = "The group name that grants admin privileges",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "squirrel-admins",
                EnvironmentVariableName = "PLUGIN_OIDC_ADMIN_GROUP"
            },
            new()
            {
                Key = "EditorGroup",
                DisplayName = "Editor Group",
                Description = "The group name that grants editor privileges",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "squirrel-editors",
                EnvironmentVariableName = "PLUGIN_OIDC_EDITOR_GROUP"
            },
            new()
            {
                Key = "AutoCreateUsers",
                DisplayName = "Auto-Create Users",
                Description = "Automatically create user accounts on first login",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "true",
                EnvironmentVariableName = "PLUGIN_OIDC_AUTO_CREATE_USERS"
            },
            new()
            {
                Key = "RequireHttpsMetadata",
                DisplayName = "Require HTTPS Metadata",
                Description = "Require HTTPS for metadata endpoint (disable for development only)",
                IsRequired = false,
                IsSecret = false,
                DefaultValue = "true",
                EnvironmentVariableName = "PLUGIN_OIDC_REQUIRE_HTTPS_METADATA"
            }
        };
    }

    public override async Task<bool> ValidateConfigurationAsync(Dictionary<string, string> configuration, CancellationToken cancellationToken = default)
    {
        // Validate required fields
        if (!configuration.ContainsKey("Authority") || string.IsNullOrWhiteSpace(configuration["Authority"]))
        {
            return false;
        }

        if (!configuration.ContainsKey("ClientId") || string.IsNullOrWhiteSpace(configuration["ClientId"]))
        {
            return false;
        }

        if (!configuration.ContainsKey("ClientSecret") || string.IsNullOrWhiteSpace(configuration["ClientSecret"]))
        {
            return false;
        }

        // Validate Authority URL format
        if (!Uri.TryCreate(configuration["Authority"], UriKind.Absolute, out var authorityUri) ||
            (authorityUri.Scheme != "http" && authorityUri.Scheme != "https"))
        {
            return false;
        }

        return await Task.FromResult(true);
    }

    public override Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        _services = services;
        return Task.CompletedTask;
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _services = null;
        return Task.CompletedTask;
    }

    public override IAuthenticationStrategy CreateStrategy(Dictionary<string, string> config)
    {
        if (_services == null)
        {
            throw new InvalidOperationException("Plugin has not been initialized. Call InitializeAsync first.");
        }

        // Get required services from the service provider
        var loggerFactory = _services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<OidcAuthenticationStrategy>();

        // Create and return the strategy with configuration
        return new OidcAuthenticationStrategy(_services, config, logger);
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
