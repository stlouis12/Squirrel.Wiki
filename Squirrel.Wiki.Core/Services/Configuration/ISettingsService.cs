using System.Threading;
using System.Threading.Tasks;

namespace Squirrel.Wiki.Core.Services.Configuration;

/// <summary>
/// Service for managing application settings stored in the database
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value by key
    /// </summary>
    /// <typeparam name="T">The type to deserialize the setting to</typeparam>
    /// <param name="key">The setting key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The setting value, or null if not found</returns>
    Task<T?> GetSettingAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a setting value
    /// </summary>
    /// <typeparam name="T">The type of the setting value</typeparam>
    /// <param name="key">The setting key</param>
    /// <param name="value">The setting value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveSettingAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all settings as a dictionary
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of all settings</returns>
    Task<Dictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a setting by key
    /// </summary>
    /// <param name="key">The setting key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a setting exists
    /// </summary>
    /// <param name="key">The setting key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the setting exists</returns>
    Task<bool> SettingExistsAsync(string key, CancellationToken cancellationToken = default);
}
