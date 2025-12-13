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

    public AuthenticationProvider Provider => AuthenticationProvider.External;

    public OidcAuthenticationStrategy(
        IServiceProvider serviceProvider,
        Dictionary<string, string> config,
        ILogger<OidcAuthenticationStrategy> logger)
    {
        _services = serviceProvider;
        _config = config;
        _logger = logger;
    }

    public bool CanHandle(AuthenticationRequest request)
    {
        return request.Provider == AuthenticationProvider.External &&
               !string.IsNullOrWhiteSpace(request.ExternalId);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(request))
        {
            return AuthenticationResult.Failed("Invalid OIDC authentication request", AuthenticationFailureReason.InvalidCredentials);
        }

        // Get configuration values
        var config = GetAuthenticationConfig();

        // Get repository and types via reflection
        var reflectionContext = GetReflectionContext();
        if (reflectionContext == null)
        {
            return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
        }

        // Find existing user
        var user = await FindUserByExternalIdAsync(request.ExternalId!, reflectionContext, cancellationToken);

        if (user == null)
        {
            return await HandleNewUserAsync(request, config, reflectionContext, cancellationToken);
        }

        return await HandleExistingUserAsync(user, request, config, reflectionContext, cancellationToken);
    }

    private OidcConfig GetAuthenticationConfig()
    {
        return new OidcConfig
        {
            AdminGroup = _config.GetValueOrDefault("AdminGroup", "squirrel-admins"),
            EditorGroup = _config.GetValueOrDefault("EditorGroup", "squirrel-editors"),
            AutoCreateUsers = bool.Parse(_config.GetValueOrDefault("AutoCreateUsers", "true"))
        };
    }

    private ReflectionContext? GetReflectionContext()
    {
        var userRepositoryType = Type.GetType("Squirrel.Wiki.Core.Database.Repositories.IUserRepository, Squirrel.Wiki.Core");
        if (userRepositoryType == null)
        {
            _logger.LogError("Could not find IUserRepository type");
            return null;
        }

        var userRepository = _services.GetService(userRepositoryType);
        if (userRepository == null)
        {
            _logger.LogError("Could not resolve IUserRepository from service provider");
            return null;
        }

        var userType = Type.GetType("Squirrel.Wiki.Core.Database.Entities.User, Squirrel.Wiki.Core");
        if (userType == null)
        {
            _logger.LogError("Could not find User entity type");
            return null;
        }

        return new ReflectionContext
        {
            UserRepository = userRepository,
            UserRepositoryType = userRepositoryType,
            UserType = userType
        };
    }

    private async Task<object?> FindUserByExternalIdAsync(
        string externalId, 
        ReflectionContext context, 
        CancellationToken cancellationToken)
    {
        var getByExternalIdMethod = context.UserRepositoryType.GetMethod("GetByExternalIdAsync");
        if (getByExternalIdMethod == null)
        {
            _logger.LogError("Could not find GetByExternalIdAsync method");
            return null;
        }

        var getUserTask = (Task?)getByExternalIdMethod.Invoke(
            context.UserRepository, 
            new object[] { externalId, cancellationToken });
        
        if (getUserTask == null)
        {
            _logger.LogError("GetByExternalIdAsync returned null task");
            return null;
        }

        await getUserTask;
        var userResultProperty = getUserTask.GetType().GetProperty("Result");
        return userResultProperty?.GetValue(getUserTask);
    }

    private async Task<AuthenticationResult> HandleNewUserAsync(
        AuthenticationRequest request,
        OidcConfig config,
        ReflectionContext context,
        CancellationToken cancellationToken)
    {
        if (!config.AutoCreateUsers)
        {
            _logger.LogWarning("User not found and auto-create is disabled: {ExternalId}", request.ExternalId);
            return AuthenticationResult.Failed("User account not found", AuthenticationFailureReason.InvalidCredentials);
        }

        var user = CreateNewUser(request, config, context.UserType);
        if (user == null)
        {
            return AuthenticationResult.Failed("Internal configuration error", AuthenticationFailureReason.ExternalProviderError);
        }

        await SaveUserAsync(user, context, cancellationToken, isNew: true);

        _logger.LogInformation("Created new user from OIDC: {Username} (External ID: {ExternalId})",
            GetProperty<string>(user, "Username"), request.ExternalId);

        return CreateSuccessResult(user);
    }

    private async Task<AuthenticationResult> HandleExistingUserAsync(
        object user,
        AuthenticationRequest request,
        OidcConfig config,
        ReflectionContext context,
        CancellationToken cancellationToken)
    {
        // Check if account is active
        var isActive = GetProperty<bool>(user, "IsActive");
        if (!isActive)
        {
            var username = GetProperty<string>(user, "Username");
            _logger.LogWarning("Authentication failed: OIDC account inactive - {Username}", username);
            return AuthenticationResult.Failed("Account is inactive", AuthenticationFailureReason.AccountInactive);
        }

        UpdateExistingUser(user, request, config);
        await SaveUserAsync(user, context, cancellationToken, isNew: false);

        _logger.LogInformation("Updated user from OIDC: {Username} (External ID: {ExternalId})",
            GetProperty<string>(user, "Username"), request.ExternalId);

        return CreateSuccessResult(user);
    }

    private object? CreateNewUser(AuthenticationRequest request, OidcConfig config, Type userType)
    {
        var user = Activator.CreateInstance(userType);
        if (user == null)
        {
            _logger.LogError("Could not create User instance");
            return null;
        }

        SetProperty(user, "Id", Guid.NewGuid());
        SetProperty(user, "ExternalId", request.ExternalId);
        SetProperty(user, "Username", request.Username ?? request.Email ?? request.ExternalId!);
        SetProperty(user, "Email", request.Email ?? string.Empty);
        SetProperty(user, "DisplayName", request.DisplayName ?? request.Username ?? "Unknown");
        SetProperty(user, "Provider", AuthenticationProvider.External);
        SetProperty(user, "IsAdmin", request.Groups.Contains(config.AdminGroup));
        SetProperty(user, "IsEditor", request.Groups.Contains(config.EditorGroup));
        SetProperty(user, "IsActive", true);
        SetProperty(user, "CreatedOn", DateTime.UtcNow);
        SetProperty(user, "LastLoginOn", DateTime.UtcNow);

        return user;
    }

    private static void UpdateExistingUser(object user, AuthenticationRequest request, OidcConfig config)
    {
        SetProperty(user, "Email", request.Email ?? GetProperty<string>(user, "Email"));
        SetProperty(user, "DisplayName", request.DisplayName ?? GetProperty<string>(user, "DisplayName"));
        SetProperty(user, "IsAdmin", request.Groups.Contains(config.AdminGroup));
        SetProperty(user, "IsEditor", request.Groups.Contains(config.EditorGroup));
        SetProperty(user, "LastLoginOn", DateTime.UtcNow);
    }

    private static async Task SaveUserAsync(
        object user, 
        ReflectionContext context, 
        CancellationToken cancellationToken, 
        bool isNew)
    {
        var methodName = isNew ? "AddAsync" : "UpdateAsync";
        var method = context.UserRepositoryType.GetMethod(methodName);
        
        if (method != null)
        {
            var task = (Task?)method.Invoke(context.UserRepository, new object[] { user, cancellationToken });
            if (task != null)
            {
                await task;
            }
        }
    }

    private static AuthenticationResult CreateSuccessResult(object user)
    {
        return AuthenticationResult.Succeeded(
            GetProperty<Guid>(user, "Id"),
            GetProperty<string>(user, "Username"),
            GetProperty<string>(user, "Email"),
            GetProperty<string>(user, "DisplayName"),
            GetProperty<bool>(user, "IsAdmin"),
            GetProperty<bool>(user, "IsEditor"));
    }

    private class OidcConfig
    {
        public string AdminGroup { get; set; } = string.Empty;
        public string EditorGroup { get; set; } = string.Empty;
        public bool AutoCreateUsers { get; set; }
    }

    private class ReflectionContext
    {
        public object UserRepository { get; set; } = null!;
        public Type UserRepositoryType { get; set; } = null!;
        public Type UserType { get; set; } = null!;
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
