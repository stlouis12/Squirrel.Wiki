namespace Squirrel.Wiki.Core.Services.Infrastructure;

/// <summary>
/// Service for bootstrapping initial admin user
/// </summary>
public interface IAdminBootstrapService
{
    /// <summary>
    /// Ensures at least one admin user exists in the system.
    /// If no admin exists, creates a default admin user.
    /// </summary>
    Task EnsureAdminExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any admin users exist in the system
    /// </summary>
    Task<bool> HasAdminUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default admin credentials (for first-time setup)
    /// </summary>
    (string Username, string Password) GetDefaultAdminCredentials();
}
