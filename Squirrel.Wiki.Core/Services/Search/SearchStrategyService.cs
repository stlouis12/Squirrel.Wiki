using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Contracts.Search;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Events.Search;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Plugins;

namespace Squirrel.Wiki.Core.Services.Search;

/// <summary>
/// Service that manages search strategies and delegates to the appropriate implementation
/// </summary>
public class SearchStrategyService : BaseService, ISearchService
{
    private readonly IPluginService _pluginService;
    private readonly DatabaseSearchStrategy _databaseStrategy;

    public SearchStrategyService(
        IPluginService pluginService,
        DatabaseSearchStrategy databaseStrategy,
        ILogger<SearchStrategyService> logger,
        ICacheService cache,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cache, eventPublisher, null, configuration)
    {
        _pluginService = pluginService;
        _databaseStrategy = databaseStrategy;
    }

    /// <summary>
    /// Gets the active search strategy (plugin-based if available, otherwise database)
    /// </summary>
    private async Task<ISearchStrategy> GetActiveStrategyAsync(CancellationToken cancellationToken = default)
    {
        // Check if there's an enabled search plugin
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync(cancellationToken);
        var enabledSearchPlugin = enabledPlugins.FirstOrDefault(p => p.PluginType == PluginType.SearchProvider.ToString());

        if (enabledSearchPlugin != null)
        {
            // Get the loaded plugin instance
            var loadedPlugin = _pluginService.GetLoadedPlugin<ISearchPlugin>(enabledSearchPlugin.PluginId);
            if (loadedPlugin != null)
            {
                LogInfo("Using search plugin: {PluginName} (Priority: {Priority})", loadedPlugin.Metadata.Name, loadedPlugin.Priority);
                return loadedPlugin.SearchStrategy;
            }
            else
            {
                LogWarning("Enabled search plugin '{PluginId}' found but could not be loaded as ISearchPlugin", enabledSearchPlugin.PluginId);
            }
        }

        LogInfo("Using default database search strategy");
        return _databaseStrategy;
    }

    public async Task<SearchResultsDto> SearchAsync(string searchTerm, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        
        var request = new SearchRequest
        {
            Query = searchTerm,
            Page = pageNumber,
            PageSize = pageSize
        };

        var response = await strategy.SearchAsync(request, cancellationToken);
        
        return MapToSearchResultsDto(response);
    }

    public async Task<SearchResultsDto> AdvancedSearchAsync(SearchQueryDto searchQuery, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        
        var request = new SearchRequest
        {
            Query = searchQuery.Query ?? string.Empty,
            Page = searchQuery.Page,
            PageSize = searchQuery.PageSize,
            CategoryIds = searchQuery.CategoryId.HasValue ? new List<int> { searchQuery.CategoryId.Value } : null,
            Tags = searchQuery.Tags,
            Author = searchQuery.Author,
            StartDate = searchQuery.StartDate,
            EndDate = searchQuery.EndDate
        };

        var response = await strategy.SearchAsync(request, cancellationToken);
        
        return MapToSearchResultsDto(response);
    }

    public async Task<SearchResultsDto> SearchInCategoryAsync(string query, int categoryId, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        
        var request = new SearchRequest
        {
            Query = query,
            Page = pageNum,
            PageSize = pageSize,
            CategoryIds = new List<int> { categoryId }
        };

        var response = await strategy.SearchAsync(request, cancellationToken);
        
        return MapToSearchResultsDto(response);
    }

    public async Task<SearchResultsDto> SearchByTagsAsync(List<string> tags, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        
        var request = new SearchRequest
        {
            Query = string.Empty,
            Page = pageNum,
            PageSize = pageSize,
            Tags = tags
        };

        var response = await strategy.SearchAsync(request, cancellationToken);
        
        return MapToSearchResultsDto(response);
    }

    public async Task<SearchResultsDto> SearchByAuthorAsync(string author, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        
        var request = new SearchRequest
        {
            Query = string.Empty,
            Page = pageNumber,
            PageSize = pageSize,
            Author = author
        };

        var response = await strategy.SearchAsync(request, cancellationToken);
        
        return MapToSearchResultsDto(response);
    }

    public async Task<SearchResultsDto> SearchByDateRangeAsync(DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        
        var request = new SearchRequest
        {
            Query = string.Empty,
            Page = pageNumber,
            PageSize = pageSize,
            StartDate = startDate,
            EndDate = endDate
        };

        var response = await strategy.SearchAsync(request, cancellationToken);
        
        return MapToSearchResultsDto(response);
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        return await strategy.GetSuggestionsAsync(partialQuery, maxSuggestions, cancellationToken);
    }

    public async Task IndexPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        LogDebug("Publishing PageIndexRequestedEvent for page {PageId}", pageId);
        await EventPublisher.PublishAsync(new PageIndexRequestedEvent(pageId, string.Empty, string.Empty), cancellationToken);
    }

    public async Task IndexPagesAsync(IEnumerable<int> pageIds, CancellationToken cancellationToken = default)
    {
        LogDebug("Publishing PagesIndexRequestedEvent for {Count} pages", pageIds.Count());
        await EventPublisher.PublishAsync(new PagesIndexRequestedEvent(pageIds), cancellationToken);
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("Publishing IndexRebuildRequestedEvent");
        await EventPublisher.PublishAsync(new IndexRebuildRequestedEvent(), cancellationToken);
    }

    public async Task RemoveFromIndexAsync(int pageId, CancellationToken cancellationToken = default)
    {
        LogDebug("Publishing PageRemovedFromIndexEvent for page {PageId}", pageId);
        await EventPublisher.PublishAsync(new PageRemovedFromIndexEvent(pageId), cancellationToken);
    }

    public async Task OptimizeIndexAsync(CancellationToken cancellationToken = default)
    {
        LogDebug("Publishing IndexOptimizationRequestedEvent");
        await EventPublisher.PublishAsync(new IndexOptimizationRequestedEvent(), cancellationToken);
    }

    public async Task<SearchIndexStatsDto> GetIndexStatsAsync(CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        var stats = await strategy.GetIndexStatsAsync(cancellationToken);
        
        return new SearchIndexStatsDto
        {
            TotalDocuments = stats.TotalDocuments,
            IndexSizeBytes = stats.IndexSizeBytes,
            LastOptimized = stats.LastOptimized ?? DateTime.MinValue,
            IsValid = stats.IsValid
        };
    }

    public async Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        LogWarning("Publishing IndexClearRequestedEvent");
        await EventPublisher.PublishAsync(new IndexClearRequestedEvent(), cancellationToken);
    }

    public async Task<bool> IsIndexValidAsync(CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        return await strategy.IsAvailableAsync(cancellationToken);
    }

    public async Task<IEnumerable<PageDto>> GetSimilarPagesAsync(int pageId, int count = 5, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        var results = await strategy.FindSimilarAsync(pageId.ToString(), count, cancellationToken);
        
        return results.Select(r => new PageDto
        {
            Id = int.Parse(r.DocumentId),
            Title = r.Title,
            Slug = r.Slug,
            CategoryId = r.CategoryId,
            CreatedBy = r.Author,
            CreatedOn = r.CreatedOn,
            ModifiedBy = r.Author,
            ModifiedOn = r.ModifiedOn,
            IsLocked = false,
            IsDeleted = false
        });
    }

    public async Task<SearchResultsDto> FuzzySearchAsync(string query, float minSimilarity = 0.7f, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var strategy = await GetActiveStrategyAsync(cancellationToken);
        
        var request = new SearchRequest
        {
            Query = query,
            Page = pageNum,
            PageSize = pageSize,
            MinSimilarity = minSimilarity
        };

        var response = await strategy.SearchAsync(request, cancellationToken);
        
        return MapToSearchResultsDto(response);
    }

    private SearchResultsDto MapToSearchResultsDto(SearchResponse response)
    {
        return new SearchResultsDto
        {
            Query = response.Query,
            TotalResults = response.TotalResults,
            Page = response.Page,
            PageSize = response.PageSize,
            TotalPages = response.TotalPages,
            Results = response.Results.Select(r => new SearchResultItemDto
            {
                PageId = int.Parse(r.DocumentId),
                Title = r.Title,
                Slug = r.Slug,
                Excerpt = r.Excerpt,
                ModifiedBy = r.Author,
                ModifiedOn = r.ModifiedOn,
                Score = r.Score
            }).ToList()
        };
    }
}
