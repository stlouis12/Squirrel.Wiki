using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Users;
using static Squirrel.Wiki.Core.Configuration.ConfigurationMetadataRegistry.ConfigurationKeys;

namespace Squirrel.Wiki.Core.Services.Infrastructure;

/// <summary>
/// Service implementation for bootstrapping initial admin user
/// </summary>
public class AdminBootstrapService : MinimalBaseService, IAdminBootstrapService
{
    private readonly IUserService _userService;
    private readonly IConfigurationService _configurationService;

    public AdminBootstrapService(
        IUserService userService,
        IConfigurationService configurationService,
        ILogger<AdminBootstrapService> logger)
        : base(logger)
    {
        _userService = userService;
        _configurationService = configurationService;
    }

    public async Task EnsureAdminExistsAsync(CancellationToken cancellationToken = default)
    {
        var adminConfig = await LoadAdminConfigurationAsync(cancellationToken);
        var hasAdmins = await HasAdminUsersAsync(cancellationToken);

        if (hasAdmins)
        {
            await HandleExistingAdminAsync(adminConfig, cancellationToken);
            return;
        }

        await CreateDefaultAdminAsync(adminConfig, cancellationToken);
    }

    /// <summary>
    /// Loads admin configuration from environment variables and configuration
    /// </summary>
    private async Task<AdminConfiguration> LoadAdminConfigurationAsync(CancellationToken cancellationToken)
    {
        return new AdminConfiguration
        {
            Username = await _configurationService.GetValueAsync<string>(SQUIRREL_ADMIN_USERNAME, cancellationToken),
            Password = await _configurationService.GetValueAsync<string>(SQUIRREL_ADMIN_PASSWORD, cancellationToken),
            Email = await _configurationService.GetValueAsync<string>(SQUIRREL_ADMIN_EMAIL, cancellationToken),
            DisplayName = await _configurationService.GetValueAsync<string>(SQUIRREL_ADMIN_DISPLAYNAME, cancellationToken)
        };
    }

    /// <summary>
    /// Handles updating existing admin user if environment variables are set
    /// </summary>
    private async Task HandleExistingAdminAsync(AdminConfiguration config, CancellationToken cancellationToken)
    {
        if (!config.HasEnvironmentVariables())
        {
            LogDebug("Admin user(s) already exist and no environment variables set, skipping bootstrap");
            return;
        }

        LogInfo("Environment variables detected. Checking if default admin user needs updating...");

        try
        {
            var defaultAdmin = await FindDefaultAdminAsync(config, cancellationToken);
            if (defaultAdmin == null)
            {
                LogDebug("Default admin user not found, environment variables will be used if new admin is created");
                return;
            }

            await UpdateAdminIfNeededAsync(defaultAdmin, config, cancellationToken);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to update default admin user from environment variables");
            // Don't throw - this is not critical, admin still exists
        }
    }

    /// <summary>
    /// Finds the default admin user by username or email
    /// </summary>
    private async Task<UserDto?> FindDefaultAdminAsync(AdminConfiguration config, CancellationToken cancellationToken)
    {
        var defaultAdmin = await _userService.GetByUsernameAsync(config.Username!, cancellationToken);
        if (defaultAdmin == null)
        {
            defaultAdmin = await _userService.GetByEmailAsync(config.Email!, cancellationToken);
        }
        return defaultAdmin;
    }

    /// <summary>
    /// Updates admin user if environment variables differ from current values
    /// </summary>
    private async Task UpdateAdminIfNeededAsync(UserDto defaultAdmin, AdminConfiguration config, CancellationToken cancellationToken)
    {
        CheckUsernameChange(defaultAdmin, config);

        var (needsUpdate, updates) = DetermineRequiredUpdates(defaultAdmin, config);

        if (!needsUpdate)
        {
            LogDebug("Default admin user already matches environment variables, no update needed");
            return;
        }

        await ApplyAdminUpdatesAsync(defaultAdmin, config, updates, cancellationToken);
    }

    /// <summary>
    /// Checks if username is being changed and logs a warning
    /// </summary>
    private void CheckUsernameChange(UserDto defaultAdmin, AdminConfiguration config)
    {
        if (!string.IsNullOrEmpty(config.Username) && defaultAdmin.Username != config.Username)
        {
            LogWarning(
                "Environment variable SQUIRREL_ADMIN_USERNAME is set to '{EnvUsername}' but existing admin has username '{CurrentUsername}'. " +
                "Username cannot be changed after creation. To use a different username, delete the existing admin user first.",
                config.Username, defaultAdmin.Username);
        }
    }

    /// <summary>
    /// Determines which fields need to be updated
    /// Returns: (needsUpdate, list of update descriptions)
    /// </summary>
    private static (bool needsUpdate, List<string> updates) DetermineRequiredUpdates(UserDto defaultAdmin, AdminConfiguration config)
    {
        var needsUpdate = false;
        var updates = new List<string>();

        if (!string.IsNullOrEmpty(config.Email) && defaultAdmin.Email != config.Email)
        {
            needsUpdate = true;
            updates.Add($"email: {defaultAdmin.Email} -> {config.Email}");
        }

        if (!string.IsNullOrEmpty(config.DisplayName) && defaultAdmin.DisplayName != config.DisplayName)
        {
            needsUpdate = true;
            updates.Add($"display name: {defaultAdmin.DisplayName} -> {config.DisplayName}");
        }

        if (!string.IsNullOrEmpty(config.Password))
        {
            needsUpdate = true;
            updates.Add("password: [updated from environment variable]");
        }

        return (needsUpdate, updates);
    }

    /// <summary>
    /// Applies the updates to the admin user
    /// </summary>
    private async Task ApplyAdminUpdatesAsync(UserDto defaultAdmin, AdminConfiguration config, List<string> updates, CancellationToken cancellationToken)
    {
        LogWarning("Updating default admin user with environment variable values: {Updates}", string.Join(", ", updates));

        var updateDto = new UserUpdateDto
        {
            Email = config.Email ?? defaultAdmin.Email,
            DisplayName = config.DisplayName ?? defaultAdmin.DisplayName,
            FirstName = defaultAdmin.FirstName,
            LastName = defaultAdmin.LastName,
            IsAdmin = defaultAdmin.IsAdmin,
            IsEditor = defaultAdmin.IsEditor
        };

        await _userService.UpdateAsync(defaultAdmin.Id, updateDto, cancellationToken);

        if (!string.IsNullOrEmpty(config.Password))
        {
            await _userService.SetPasswordAsync(defaultAdmin.Id, config.Password, cancellationToken);
        }

        LogInfo("Default admin user updated successfully from environment variables");
    }

    /// <summary>
    /// Creates a new default admin user
    /// </summary>
    private async Task CreateDefaultAdminAsync(AdminConfiguration config, CancellationToken cancellationToken)
    {
        LogWarning("No admin users found. Creating default admin user...");

        try
        {
            var adminUser = await _userService.CreateLocalUserAsync(
                username: config.Username!,
                email: config.Email!,
                password: config.Password!,
                displayName: config.DisplayName!,
                isAdmin: true,
                isEditor: true,
                cancellationToken: cancellationToken
            );

            LogWarning(
                "Default admin user created successfully. " +
                "Username: {Username}, Password: {PasswordSource}. " +
                "IMPORTANT: Change this password immediately after first login!",
                config.Username,
                config.Password == "Squirrel123!" ? "[default]" : "[from configuration]"
            );

            LogInfo("Admin user created with ID: {UserId}", adminUser.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to create default admin user");
            throw;
        }
    }

    /// <summary>
    /// Helper class to hold admin configuration
    /// </summary>
    private class AdminConfiguration
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }

        public bool HasEnvironmentVariables()
        {
            return !string.IsNullOrEmpty(Username) ||
                   !string.IsNullOrEmpty(Password) ||
                   !string.IsNullOrEmpty(Email);
        }
    }

    public async Task<bool> HasAdminUsersAsync(CancellationToken cancellationToken = default)
    {
        var admins = await _userService.GetAdminsAsync(cancellationToken);
        return admins.Any();
    }

    public (string Username, string Password) GetDefaultAdminCredentials()
    {
        // Note: This is a synchronous method but configuration service is async
        // In practice, this method may need to be made async or use .GetAwaiter().GetResult()
        // For now, we'll use the synchronous approach with GetAwaiter().GetResult()
        var username = _configurationService.GetValueAsync<string>(SQUIRREL_ADMIN_USERNAME, CancellationToken.None).GetAwaiter().GetResult();
        var password = _configurationService.GetValueAsync<string>(SQUIRREL_ADMIN_PASSWORD, CancellationToken.None).GetAwaiter().GetResult();
        return (username!, password!);
    }
}
