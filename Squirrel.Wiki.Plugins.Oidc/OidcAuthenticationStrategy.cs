using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Authentication;

namespace Squirrel.Wiki.Plugins.Oidc;

/// <summary>
/// Authentication strategy for OpenID Connect authentication
/// This is the plugin version that uses IServiceProvider to access Core services
/// </summary>
public class OidcAuthenticationStrategy : IAuthenticationStrategy
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, string> _config;
    private readonly ILogger<OidcAuthenticationStrategy> _logger;

    public AuthenticationProvider Provider => AuthenticationProvider.OpenIdConnect;

    public OidcAuthenticationStrategy(
        IServiceProvider services,
        Dictionary<string, string> config,
        ILogger<OidcAuthenticationStrategy> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    public bool CanHandle(AuthenticationRequest request)
    {
        return request.Provider == AuthenticationProvider.OpenIdConnect &&
               !string.IsNullOrWhiteSpace(request.ExternalId);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
        {
            return AuthenticationResult.Failed("Invalid OIDC authentication request", AuthenticationFailureReason.InvalidCredentials);
        }

        // Get configuration values
        var adminGroup = _config.GetValueOrDefault("AdminGroup", "squirrel-admins");
        var editorGroup = _config.GetValueOrDefault("EditorGroup", "squirrel-editors");
        var autoCreateUsers = bool.Parse(_config.GetValueOrDefault("AutoCreateUsers", "true"));

        // Use reflection to get IUserRepository from Core
        // This avoids circular dependency while still allowing the plugin to work
        var userRepositoryType = Type.GetType("Squirrel.Wiki.Core.Database.Repositories.IUserRepository, Squirrel.Wiki.Core");
        if (userRepositoryType == null)
        {
            _logger.LogError("Could not find IUserRepository type");
            return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
        }

        var userRepository = _services.GetService(userRepositoryType);
        if (userRepository == null)
        {
            _logger.LogError("Could not resolve IUserRepository from service provider");
            return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
        }

        // Get the User entity type
        var userType = Type.GetType("Squirrel.Wiki.Core.Database.Entities.User, Squirrel.Wiki.Core");
        if (userType == null)
        {
            _logger.LogError("Could not find User entity type");
            return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
        }

        // Call GetByExternalIdAsync using reflection
        var getByExternalIdMethod = userRepositoryType.GetMethod("GetByExternalIdAsync");
        if (getByExternalIdMethod == null)
        {
            _logger.LogError("Could not find GetByExternalIdAsync method");
            return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
        }

        var getUserTask = (Task?)getByExternalIdMethod.Invoke(userRepository, new object[] { request.ExternalId!, cancellationToken });
        if (getUserTask == null)
        {
            _logger.LogError("GetByExternalIdAsync returned null task");
            return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
        }

        await getUserTask;
        var userResultProperty = getUserTask.GetType().GetProperty("Result");
        var user = userResultProperty?.GetValue(getUserTask);

        if (user == null)
        {
            // Create new user if auto-create is enabled
            if (!autoCreateUsers)
            {
                _logger.LogWarning("User not found and auto-create is disabled: {ExternalId}", request.ExternalId);
                return AuthenticationResult.Failed("User account not found", AuthenticationFailureReason.InvalidCredentials);
            }

            // Create new user using reflection
            user = Activator.CreateInstance(userType);
            if (user == null)
            {
                _logger.LogError("Could not create User instance");
                return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
            }

            // Set user properties
            SetProperty(user, "Id", Guid.NewGuid());
            SetProperty(user, "ExternalId", request.ExternalId);
            SetProperty(user, "Username", request.Username ?? request.Email ?? request.ExternalId!);
            SetProperty(user, "Email", request.Email ?? string.Empty);
            SetProperty(user, "DisplayName", request.DisplayName ?? request.Username ?? "Unknown");
            SetProperty(user, "Provider", AuthenticationProvider.OpenIdConnect);
            SetProperty(user, "IsAdmin", request.Groups.Contains(adminGroup));
            SetProperty(user, "IsEditor", request.Groups.Contains(editorGroup));
            SetProperty(user, "IsActive", true);
            SetProperty(user, "CreatedOn", DateTime.UtcNow);
            SetProperty(user, "LastLoginOn", DateTime.UtcNow);

            // Add user
            var addMethod = userRepositoryType.GetMethod("AddAsync");
            if (addMethod != null)
            {
                var addTask = (Task?)addMethod.Invoke(userRepository, new object[] { user, cancellationToken });
                if (addTask != null)
                {
                    await addTask;
                }
            }

            _logger.LogInformation("Created new user from OIDC: {Username} (External ID: {ExternalId})",
                GetProperty<string>(user, "Username"), request.ExternalId);
        }
        else
        {
            // Check if account is active
            var isActive = GetProperty<bool>(user, "IsActive");
            if (!isActive)
            {
                var username = GetProperty<string>(user, "Username");
                _logger.LogWarning("Authentication failed: OIDC account inactive - {Username}", username);
                return AuthenticationResult.Failed("Account is inactive", AuthenticationFailureReason.AccountInactive);
            }

            // Update existing user
            SetProperty(user, "Email", request.Email ?? GetProperty<string>(user, "Email"));
            SetProperty(user, "DisplayName", request.DisplayName ?? GetProperty<string>(user, "DisplayName"));
            SetProperty(user, "IsAdmin", request.Groups.Contains(adminGroup));
            SetProperty(user, "IsEditor", request.Groups.Contains(editorGroup));
            SetProperty(user, "LastLoginOn", DateTime.UtcNow);

            // Update user
            var updateMethod = userRepositoryType.GetMethod("UpdateAsync");
            if (updateMethod != null)
            {
                var updateTask = (Task?)updateMethod.Invoke(userRepository, new object[] { user, cancellationToken });
                if (updateTask != null)
                {
                    await updateTask;
                }
            }

            _logger.LogInformation("Updated user from OIDC: {Username} (External ID: {ExternalId})",
                GetProperty<string>(user, "Username"), request.ExternalId);
        }

        // Return success result
        return AuthenticationResult.Succeeded(
            GetProperty<Guid>(user, "Id"),
            GetProperty<string>(user, "Username"),
            GetProperty<string>(user, "Email"),
            GetProperty<string>(user, "DisplayName"),
            GetProperty<bool>(user, "IsAdmin"),
            GetProperty<bool>(user, "IsEditor"));
    }

    private static void SetProperty(object obj, string propertyName, object? value)
    {
        var property = obj.GetType().GetProperty(propertyName);
        property?.SetValue(obj, value);
    }

    private static T GetProperty<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(propertyName);
        var value = property?.GetValue(obj);
        return value != null ? (T)value : default!;
    }
}
