using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Users;

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
        // Get credentials from configuration (which checks environment variables, then database, then defaults)
        var envUsername = await _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_USERNAME", cancellationToken);
        var envPassword = await _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_PASSWORD", cancellationToken);
        var envEmail = await _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_EMAIL", cancellationToken);
        var envDisplayName = await _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_DISPLAYNAME", cancellationToken);

        // Check if any admin users exist
        var hasAdmins = await HasAdminUsersAsync(cancellationToken);

        if (hasAdmins)
        {
            // If environment variables are set, update the existing default admin user
            if (!string.IsNullOrEmpty(envUsername) || !string.IsNullOrEmpty(envPassword) || !string.IsNullOrEmpty(envEmail))
            {
                LogInfo("Environment variables detected. Checking if default admin user needs updating...");
                
                try
                {
                    // Try to find the default admin user by username or email
                    // Use the configured default values to find the existing admin
                    var defaultUsername = await _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_USERNAME", cancellationToken);
                    var defaultEmail = await _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_EMAIL", cancellationToken);
                    
                    var defaultAdmin = await _userService.GetByUsernameAsync(defaultUsername!, cancellationToken);
                    if (defaultAdmin == null)
                    {
                        defaultAdmin = await _userService.GetByEmailAsync(defaultEmail!, cancellationToken);
                    }

                    if (defaultAdmin != null)
                    {
                        var needsUpdate = false;
                        var updates = new List<string>();

                        // Note: Username cannot be updated through UserUpdateDto
                        // If username needs to change, admin should create a new user
                        if (!string.IsNullOrEmpty(envUsername) && defaultAdmin.Username != envUsername)
                        {
                            LogWarning(
                                "Environment variable SQUIRREL_ADMIN_USERNAME is set to '{EnvUsername}' but existing admin has username '{CurrentUsername}'. " +
                                "Username cannot be changed after creation. To use a different username, delete the existing admin user first.",
                                envUsername, defaultAdmin.Username);
                        }

                        // Update email if environment variable is set and different
                        if (!string.IsNullOrEmpty(envEmail) && defaultAdmin.Email != envEmail)
                        {
                            needsUpdate = true;
                            updates.Add($"email: {defaultAdmin.Email} -> {envEmail}");
                        }

                        // Update display name if environment variable is set and different
                        if (!string.IsNullOrEmpty(envDisplayName) && defaultAdmin.DisplayName != envDisplayName)
                        {
                            needsUpdate = true;
                            updates.Add($"display name: {defaultAdmin.DisplayName} -> {envDisplayName}");
                        }

                        // Always update password if environment variable is set (we can't compare hashed passwords)
                        if (!string.IsNullOrEmpty(envPassword))
                        {
                            needsUpdate = true;
                            updates.Add("password: [updated from environment variable]");
                        }

                        if (needsUpdate)
                        {
                            LogWarning("Updating default admin user with environment variable values: {Updates}", string.Join(", ", updates));

                            // Update the user details (email, display name, etc.)
                            var updateDto = new UserUpdateDto
                            {
                                Email = envEmail ?? defaultAdmin.Email,
                                DisplayName = envDisplayName ?? defaultAdmin.DisplayName,
                                FirstName = defaultAdmin.FirstName,
                                LastName = defaultAdmin.LastName,
                                IsAdmin = defaultAdmin.IsAdmin,
                                IsEditor = defaultAdmin.IsEditor
                            };

                            await _userService.UpdateAsync(defaultAdmin.Id, updateDto, cancellationToken);

                            // Update password if provided
                            if (!string.IsNullOrEmpty(envPassword))
                            {
                                await _userService.SetPasswordAsync(defaultAdmin.Id, envPassword, cancellationToken);
                            }

                            LogInfo("Default admin user updated successfully from environment variables");
                        }
                        else
                        {
                            LogDebug("Default admin user already matches environment variables, no update needed");
                        }
                    }
                    else
                    {
                        LogDebug("Default admin user not found, environment variables will be used if new admin is created");
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex, "Failed to update default admin user from environment variables");
                    // Don't throw - this is not critical, admin still exists
                }
            }
            else
            {
                LogDebug("Admin user(s) already exist and no environment variables set, skipping bootstrap");
            }
            
            return;
        }

        LogWarning("No admin users found. Creating default admin user...");

        // Configuration service already handles defaults, so we can use the values directly
        var username = envUsername!;
        var password = envPassword!;
        var email = envEmail!;
        var displayName = envDisplayName!;

        try
        {
            // Create the admin user
            var adminUser = await _userService.CreateLocalUserAsync(
                username: username,
                email: email,
                password: password,
                displayName: displayName,
                isAdmin: true,
                isEditor: true,
                cancellationToken: cancellationToken
            );

            LogWarning(
                "Default admin user created successfully. " +
                "Username: {Username}, Password: {PasswordSource}. " +
                "IMPORTANT: Change this password immediately after first login!",
                username,
                password == "Squirrel123!" ? "[default]" : "[from configuration]"
            );

            LogInfo("Admin user created with ID: {UserId}", adminUser.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to create default admin user");
            throw;
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
        var username = _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_USERNAME", CancellationToken.None).GetAwaiter().GetResult();
        var password = _configurationService.GetValueAsync<string>("SQUIRREL_ADMIN_PASSWORD", CancellationToken.None).GetAwaiter().GetResult();
        return (username!, password!);
    }
}
