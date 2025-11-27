using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Contracts.Search;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Plugins;

namespace Squirrel.Wiki.Plugins.Lucene;

/// <summary>
/// Lucene.NET search plugin for high-performance full-text search
/// </summary>
public class LuceneSearchPlugin : PluginBase, ISearchPlugin
{
    private LuceneSearchStrategy? _searchStrategy;
    private ILogger<LuceneSearchPlugin>? _logger;
    private ILoggerFactory? _loggerFactory;
    private List<IPluginAction>? _actions;

    public ISearchStrategy SearchStrategy => _searchStrategy ?? throw new InvalidOperationException("Plugin not initialized");
    
    public int Priority => 100;
    public bool SupportsRealTimeIndexing => true;
    public bool SupportsFuzzySearch => true;
    public bool SupportsFacetedSearch => false;
    public bool SupportsSuggestions => true;
    public bool SupportsSimilaritySearch => true;
    public int? MaxDocuments => null; // No hard limit

    public LuceneSearchPlugin()
    {
        // Dependencies will be resolved during InitializeAsync
    }

    public override PluginMetadata Metadata => new()
    {
        Id = "squirrel.wiki.plugins.lucene",
        Name = "Lucene.NET Search",
        Description = "High-performance full-text search using Lucene.NET. Provides fast, scalable search with advanced features like fuzzy matching, field boosting, and relevance scoring.",
        Version = "1.0.0",
        Author = "Squirrel Wiki",
        Type = PluginType.SearchProvider,
        IsCorePlugin = true,
        RequiresConfiguration = false,
        Configuration = Array.Empty<PluginConfigurationItem>()
    };

    public override IEnumerable<PluginConfigurationItem> GetConfigurationSchema()
    {
        return Metadata.Configuration ?? Enumerable.Empty<PluginConfigurationItem>();
    }

    public override async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(services, cancellationToken);
        
        // Resolve dependencies from service provider
        _loggerFactory = services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<LuceneSearchPlugin>();
        
        // Create the search strategy
        var strategyLogger = _loggerFactory.CreateLogger<LuceneSearchStrategy>();
        _searchStrategy = new LuceneSearchStrategy(strategyLogger);
        
        _logger.LogInformation("Initializing Lucene search plugin");

        try
        {
            // Get index path from SQUIRREL_APP_DATA_PATH
            var appDataPath = Configuration != null
                ? await Configuration.GetValueAsync<string>("SQUIRREL_APP_DATA_PATH")
                : null;
            
            if (string.IsNullOrWhiteSpace(appDataPath))
            {
                appDataPath = "App_Data";
                _logger.LogWarning("SQUIRREL_APP_DATA_PATH not configured, using default: {AppDataPath}", appDataPath);
            }
            
            // Make the path absolute if it's relative
            // Use AppContext.BaseDirectory (bin/Debug/net8.0) instead of ContentRootPath (project directory)
            if (!Path.IsPathRooted(appDataPath))
            {
                appDataPath = Path.Combine(AppContext.BaseDirectory, appDataPath);
            }
            
            var indexPath = Path.Combine(appDataPath, "SearchIndex");
            _logger.LogInformation("Using index path: {IndexPath}", indexPath);

            // Ensure the directory exists
            Directory.CreateDirectory(indexPath);

            // Initialize the search strategy
            var config = new SearchConfiguration
            {
                IndexPath = indexPath,
                Options = new Dictionary<string, string>
                {
                    { "LuceneVersion", "4.8.0" },
                    { "Analyzer", "StandardAnalyzer" }
                }
            };

            await _searchStrategy.InitializeAsync(config, cancellationToken);

            _logger.LogInformation("Lucene search plugin initialized successfully with index path: {IndexPath}", indexPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Lucene search plugin");
            throw;
        }
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down Lucene search plugin");
        return Task.CompletedTask;
    }

    public async Task<PluginValidationResult> ValidateSearchConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _searchStrategy.IsAvailableAsync(cancellationToken);
            if (!isAvailable)
            {
                return PluginValidationResult.SuccessWithWarnings(
                    "Search index is not yet available. It will be created when the first document is indexed.");
            }

            var stats = await _searchStrategy.GetIndexStatsAsync(cancellationToken);
            if (!stats.IsValid)
            {
                return PluginValidationResult.Failed("Search index is not valid or accessible.");
            }

            return PluginValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Lucene search configuration");
            return PluginValidationResult.Failed($"Validation error: {ex.Message}");
        }
    }

    public override IEnumerable<IPluginAction> GetActions()
    {
        // Initialize actions if we have all dependencies
        if (_actions == null && _searchStrategy != null && _loggerFactory != null)
        {
            _actions = new List<IPluginAction>
            {
                new RebuildIndexAction(
                    _searchStrategy,
                    _loggerFactory.CreateLogger<RebuildIndexAction>()),
                    
                new OptimizeIndexAction(
                    _searchStrategy,
                    _loggerFactory.CreateLogger<OptimizeIndexAction>()),
                    
                new ClearIndexAction(
                    _searchStrategy,
                    _loggerFactory.CreateLogger<ClearIndexAction>()),
                    
                new GetIndexStatsAction(
                    _searchStrategy,
                    _loggerFactory.CreateLogger<GetIndexStatsAction>())
            };
        }

        // Return initialized actions if available, otherwise return empty list
        // Actions will only be executable after plugin initialization
        return _actions ?? Enumerable.Empty<IPluginAction>();
    }
}
