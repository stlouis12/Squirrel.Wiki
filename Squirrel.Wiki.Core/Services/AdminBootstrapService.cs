using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Service implementation for bootstrapping initial admin user
/// </summary>
public class AdminBootstrapService : IAdminBootstrapService
{
    private readonly IUserService _userService;
    private readonly ILogger<AdminBootstrapService> _logger;

    private const string DefaultUsername = "admin";
    private const string DefaultPassword = "Squirrel123!"; // Should be changed on first login
    private const string DefaultEmail = "admin@localhost";
    private const string DefaultDisplayName = "Administrator";

    public AdminBootstrapService(
        IUserService userService,
        ILogger<AdminBootstrapService> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task EnsureAdminExistsAsync(CancellationToken cancellationToken = default)
    {
        // Get credentials from environment variables
        var envUsername = Environment.GetEnvironmentVariable("SQUIRREL_ADMIN_USERNAME");
        var envPassword = Environment.GetEnvironmentVariable("SQUIRREL_ADMIN_PASSWORD");
        var envEmail = Environment.GetEnvironmentVariable("SQUIRREL_ADMIN_EMAIL");
        var envDisplayName = Environment.GetEnvironmentVariable("SQUIRREL_ADMIN_DISPLAYNAME");

        // Check if any admin users exist
        var hasAdmins = await HasAdminUsersAsync(cancellationToken);

        if (hasAdmins)
        {
            // If environment variables are set, update the existing default admin user
            if (!string.IsNullOrEmpty(envUsername) || !string.IsNullOrEmpty(envPassword) || !string.IsNullOrEmpty(envEmail))
            {
                _logger.LogInformation("Environment variables detected. Checking if default admin user needs updating...");
                
                try
                {
                    // Try to find the default admin user by username or email
                    var defaultAdmin = await _userService.GetByUsernameAsync(DefaultUsername, cancellationToken);
                    if (defaultAdmin == null)
                    {
                        defaultAdmin = await _userService.GetByEmailAsync(DefaultEmail, cancellationToken);
                    }

                    if (defaultAdmin != null)
                    {
                        var needsUpdate = false;
                        var updates = new List<string>();

                        // Note: Username cannot be updated through UserUpdateDto
                        // If username needs to change, admin should create a new user
                        if (!string.IsNullOrEmpty(envUsername) && defaultAdmin.Username != envUsername)
                        {
                            _logger.LogWarning(
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
                            _logger.LogWarning("Updating default admin user with environment variable values: {Updates}", string.Join(", ", updates));

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

                            _logger.LogInformation("Default admin user updated successfully from environment variables");
                        }
                        else
                        {
                            _logger.LogDebug("Default admin user already matches environment variables, no update needed");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Default admin user not found, environment variables will be used if new admin is created");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update default admin user from environment variables");
                    // Don't throw - this is not critical, admin still exists
                }
            }
            else
            {
                _logger.LogDebug("Admin user(s) already exist and no environment variables set, skipping bootstrap");
            }
            
            return;
        }

        _logger.LogWarning("No admin users found. Creating default admin user...");

        // Use environment variables or fall back to defaults
        var username = envUsername ?? DefaultUsername;
        var password = envPassword ?? DefaultPassword;
        var email = envEmail ?? DefaultEmail;
        var displayName = envDisplayName ?? DefaultDisplayName;

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

            _logger.LogWarning(
                "Default admin user created successfully. " +
                "Username: {Username}, Password: {PasswordSource}. " +
                "IMPORTANT: Change this password immediately after first login!",
                username,
                password == DefaultPassword ? "[default: admin]" : "[from environment variable]"
            );

            _logger.LogInformation("Admin user created with ID: {UserId}", adminUser.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create default admin user");
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
        var username = Environment.GetEnvironmentVariable("SQUIRREL_ADMIN_USERNAME") ?? DefaultUsername;
        var password = Environment.GetEnvironmentVariable("SQUIRREL_ADMIN_PASSWORD") ?? DefaultPassword;
        return (username, password);
    }
}
