using Squirrel.Wiki.Contracts.Search;

namespace Squirrel.Wiki.Contracts.Plugins;

/// <summary>
/// Defines the contract for search plugins.
/// Search plugins provide different search engine implementations (Lucene, Elasticsearch, Azure Search, etc.)
/// </summary>
public interface ISearchPlugin : IPlugin
{
    /// <summary>
    /// Gets the search strategy implementation provided by this plugin
    /// </summary>
    ISearchStrategy SearchStrategy { get; }

    /// <summary>
    /// Gets the priority of this search plugin (higher priority plugins are preferred)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Gets whether this search plugin supports real-time indexing
    /// </summary>
    bool SupportsRealTimeIndexing { get; }

    /// <summary>
    /// Gets whether this search plugin supports fuzzy search
    /// </summary>
    bool SupportsFuzzySearch { get; }

    /// <summary>
    /// Gets whether this search plugin supports faceted search
    /// </summary>
    bool SupportsFacetedSearch { get; }

    /// <summary>
    /// Gets whether this search plugin supports suggestions/autocomplete
    /// </summary>
    bool SupportsSuggestions { get; }

    /// <summary>
    /// Gets whether this search plugin supports similarity search
    /// </summary>
    bool SupportsSimilaritySearch { get; }

    /// <summary>
    /// Gets the maximum number of documents this search plugin can handle efficiently
    /// </summary>
    int? MaxDocuments { get; }

    /// <summary>
    /// Validates the plugin configuration for search functionality
    /// </summary>
    Task<PluginValidationResult> ValidateSearchConfigurationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of plugin validation
/// </summary>
public class PluginValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation error messages
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warning messages
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static PluginValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with errors
    /// </summary>
    public static PluginValidationResult Failed(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };

    /// <summary>
    /// Creates a successful validation result with warnings
    /// </summary>
    public static PluginValidationResult SuccessWithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings.ToList()
    };
}
