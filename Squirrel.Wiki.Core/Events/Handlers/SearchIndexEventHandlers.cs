using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Contracts.Search;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events.Search;
using Squirrel.Wiki.Core.Services.Plugins;

namespace Squirrel.Wiki.Core.Events.Handlers;

/// <summary>
/// Handles page index requested events and notifies search plugins
/// </summary>
public class PageIndexRequestedEventHandler : IEventHandler<PageIndexRequestedEvent>
{
    private readonly IPluginService _pluginService;
    private readonly IPageRepository _pageRepository;
    private readonly ILogger<PageIndexRequestedEventHandler> _logger;

    public PageIndexRequestedEventHandler(
        IPluginService pluginService,
        IPageRepository pageRepository,
        ILogger<PageIndexRequestedEventHandler> logger)
    {
        _pluginService = pluginService;
        _pageRepository = pageRepository;
        _logger = logger;
    }

    public async Task HandleAsync(PageIndexRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling PageIndexRequestedEvent for page {PageId}", domainEvent.PageId);

        // Get enabled search plugins
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync(cancellationToken);
        var searchPlugins = enabledPlugins
            .Where(p => p.PluginType == PluginType.SearchProvider.ToString())
            .Select(p => _pluginService.GetLoadedPlugin<ISearchPlugin>(p.PluginId))
            .Where(p => p != null)
            .Cast<ISearchPlugin>()
            .ToList();

        if (!searchPlugins.Any())
        {
            _logger.LogDebug("No enabled search plugins found");
            return;
        }

        // Get page data
        var page = await _pageRepository.GetByIdAsync(domainEvent.PageId, cancellationToken);
        if (page == null)
        {
            _logger.LogWarning("Page {PageId} not found for indexing", domainEvent.PageId);
            return;
        }

        var latestContent = await _pageRepository.GetLatestContentAsync(domainEvent.PageId, cancellationToken);
        if (latestContent == null)
        {
            _logger.LogWarning("No content found for page {PageId}", domainEvent.PageId);
            return;
        }

        // Create search document
        var document = new SearchDocument
        {
            Id = page.Id.ToString(),
            Title = page.Title,
            Slug = page.Slug,
            Content = latestContent.Text ?? string.Empty,
            CategoryId = page.CategoryId,
            CategoryName = page.Category?.Name,
            Tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>(),
            Author = page.ModifiedBy ?? page.CreatedBy,
            CreatedOn = page.CreatedOn,
            ModifiedOn = page.ModifiedOn
        };

        // Index in all enabled search plugins
        foreach (var plugin in searchPlugins)
        {
            try
            {
                _logger.LogDebug("Indexing page {PageId} in plugin {PluginName}", domainEvent.PageId, plugin.Metadata.Name);
                await plugin.SearchStrategy.IndexDocumentAsync(document, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing page {PageId} in plugin {PluginName}", domainEvent.PageId, plugin.Metadata.Name);
            }
        }
    }
}

/// <summary>
/// Handles pages index requested events and notifies search plugins
/// </summary>
public class PagesIndexRequestedEventHandler : IEventHandler<PagesIndexRequestedEvent>
{
    private readonly IPluginService _pluginService;
    private readonly IPageRepository _pageRepository;
    private readonly ILogger<PagesIndexRequestedEventHandler> _logger;

    public PagesIndexRequestedEventHandler(
        IPluginService pluginService,
        IPageRepository pageRepository,
        ILogger<PagesIndexRequestedEventHandler> logger)
    {
        _pluginService = pluginService;
        _pageRepository = pageRepository;
        _logger = logger;
    }

    public async Task HandleAsync(PagesIndexRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling PagesIndexRequestedEvent for {Count} pages", domainEvent.PageIds.Count());

        // Get enabled search plugins
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync(cancellationToken);
        var searchPlugins = enabledPlugins
            .Where(p => p.PluginType == PluginType.SearchProvider.ToString())
            .Select(p => _pluginService.GetLoadedPlugin<ISearchPlugin>(p.PluginId))
            .Where(p => p != null)
            .Cast<ISearchPlugin>()
            .ToList();

        if (!searchPlugins.Any())
        {
            _logger.LogDebug("No enabled search plugins found");
            return;
        }

        // Get page data and create documents
        var documents = new List<SearchDocument>();
        foreach (var pageId in domainEvent.PageIds)
        {
            var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
            if (page == null) continue;

            var latestContent = await _pageRepository.GetLatestContentAsync(pageId, cancellationToken);
            if (latestContent == null) continue;

            documents.Add(new SearchDocument
            {
                Id = page.Id.ToString(),
                Title = page.Title,
                Slug = page.Slug,
                Content = latestContent.Text ?? string.Empty,
                CategoryId = page.CategoryId,
                CategoryName = page.Category?.Name,
                Tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>(),
                Author = page.ModifiedBy ?? page.CreatedBy,
                CreatedOn = page.CreatedOn,
                ModifiedOn = page.ModifiedOn
            });
        }

        // Index in all enabled search plugins
        foreach (var plugin in searchPlugins)
        {
            try
            {
                _logger.LogDebug("Indexing {Count} pages in plugin {PluginName}", documents.Count, plugin.Metadata.Name);
                await plugin.SearchStrategy.IndexDocumentsAsync(documents, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing pages in plugin {PluginName}", plugin.Metadata.Name);
            }
        }
    }
}

/// <summary>
/// Handles page removed from index events and notifies search plugins
/// </summary>
public class PageRemovedFromIndexEventHandler : IEventHandler<PageRemovedFromIndexEvent>
{
    private readonly IPluginService _pluginService;
    private readonly ILogger<PageRemovedFromIndexEventHandler> _logger;

    public PageRemovedFromIndexEventHandler(
        IPluginService pluginService,
        ILogger<PageRemovedFromIndexEventHandler> logger)
    {
        _pluginService = pluginService;
        _logger = logger;
    }

    public async Task HandleAsync(PageRemovedFromIndexEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling PageRemovedFromIndexEvent for page {PageId}", domainEvent.PageId);

        // Get enabled search plugins
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync(cancellationToken);
        var searchPlugins = enabledPlugins
            .Where(p => p.PluginType == PluginType.SearchProvider.ToString())
            .Select(p => _pluginService.GetLoadedPlugin<ISearchPlugin>(p.PluginId))
            .Where(p => p != null)
            .Cast<ISearchPlugin>()
            .ToList();

        if (!searchPlugins.Any())
        {
            _logger.LogDebug("No enabled search plugins found");
            return;
        }

        // Remove from all enabled search plugins
        foreach (var plugin in searchPlugins)
        {
            try
            {
                _logger.LogDebug("Removing page {PageId} from plugin {PluginName}", domainEvent.PageId, plugin.Metadata.Name);
                await plugin.SearchStrategy.RemoveDocumentAsync(domainEvent.PageId.ToString(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing page {PageId} from plugin {PluginName}", domainEvent.PageId, plugin.Metadata.Name);
            }
        }
    }
}

/// <summary>
/// Handles index rebuild requested events and notifies search plugins
/// </summary>
public class IndexRebuildRequestedEventHandler : IEventHandler<IndexRebuildRequestedEvent>
{
    private readonly IPluginService _pluginService;
    private readonly ILogger<IndexRebuildRequestedEventHandler> _logger;

    public IndexRebuildRequestedEventHandler(
        IPluginService pluginService,
        ILogger<IndexRebuildRequestedEventHandler> logger)
    {
        _pluginService = pluginService;
        _logger = logger;
    }

    public async Task HandleAsync(IndexRebuildRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling IndexRebuildRequestedEvent requested by {RequestedBy}", domainEvent.RequestedBy ?? "System");

        // Get enabled search plugins
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync(cancellationToken);
        var searchPlugins = enabledPlugins
            .Where(p => p.PluginType == PluginType.SearchProvider.ToString())
            .Select(p => _pluginService.GetLoadedPlugin<ISearchPlugin>(p.PluginId))
            .Where(p => p != null)
            .Cast<ISearchPlugin>()
            .ToList();

        if (!searchPlugins.Any())
        {
            _logger.LogDebug("No enabled search plugins found");
            return;
        }

        // Rebuild index in all enabled search plugins
        foreach (var plugin in searchPlugins)
        {
            try
            {
                _logger.LogInformation("Rebuilding index in plugin {PluginName}", plugin.Metadata.Name);
                await plugin.SearchStrategy.RebuildIndexAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rebuilding index in plugin {PluginName}", plugin.Metadata.Name);
            }
        }
    }
}

/// <summary>
/// Handles index optimization requested events and notifies search plugins
/// </summary>
public class IndexOptimizationRequestedEventHandler : IEventHandler<IndexOptimizationRequestedEvent>
{
    private readonly IPluginService _pluginService;
    private readonly ILogger<IndexOptimizationRequestedEventHandler> _logger;

    public IndexOptimizationRequestedEventHandler(
        IPluginService pluginService,
        ILogger<IndexOptimizationRequestedEventHandler> logger)
    {
        _pluginService = pluginService;
        _logger = logger;
    }

    public async Task HandleAsync(IndexOptimizationRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling IndexOptimizationRequestedEvent requested by {RequestedBy}", domainEvent.RequestedBy ?? "System");

        // Get enabled search plugins
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync(cancellationToken);
        var searchPlugins = enabledPlugins
            .Where(p => p.PluginType == PluginType.SearchProvider.ToString())
            .Select(p => _pluginService.GetLoadedPlugin<ISearchPlugin>(p.PluginId))
            .Where(p => p != null)
            .Cast<ISearchPlugin>()
            .ToList();

        if (!searchPlugins.Any())
        {
            _logger.LogDebug("No enabled search plugins found");
            return;
        }

        // Optimize index in all enabled search plugins
        foreach (var plugin in searchPlugins)
        {
            try
            {
                _logger.LogDebug("Optimizing index in plugin {PluginName}", plugin.Metadata.Name);
                await plugin.SearchStrategy.OptimizeIndexAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing index in plugin {PluginName}", plugin.Metadata.Name);
            }
        }
    }
}

/// <summary>
/// Handles index clear requested events and notifies search plugins
/// </summary>
public class IndexClearRequestedEventHandler : IEventHandler<IndexClearRequestedEvent>
{
    private readonly IPluginService _pluginService;
    private readonly ILogger<IndexClearRequestedEventHandler> _logger;

    public IndexClearRequestedEventHandler(
        IPluginService pluginService,
        ILogger<IndexClearRequestedEventHandler> logger)
    {
        _pluginService = pluginService;
        _logger = logger;
    }

    public async Task HandleAsync(IndexClearRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Handling IndexClearRequestedEvent requested by {RequestedBy}", domainEvent.RequestedBy ?? "System");

        // Get enabled search plugins
        var enabledPlugins = await _pluginService.GetEnabledPluginsAsync(cancellationToken);
        var searchPlugins = enabledPlugins
            .Where(p => p.PluginType == PluginType.SearchProvider.ToString())
            .Select(p => _pluginService.GetLoadedPlugin<ISearchPlugin>(p.PluginId))
            .Where(p => p != null)
            .Cast<ISearchPlugin>()
            .ToList();

        if (!searchPlugins.Any())
        {
            _logger.LogDebug("No enabled search plugins found");
            return;
        }

        // Clear index in all enabled search plugins
        foreach (var plugin in searchPlugins)
        {
            try
            {
                _logger.LogWarning("Clearing index in plugin {PluginName}", plugin.Metadata.Name);
                await plugin.SearchStrategy.ClearIndexAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing index in plugin {PluginName}", plugin.Metadata.Name);
            }
        }
    }
}
