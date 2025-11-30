using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Contracts.Search;

namespace Squirrel.Wiki.Plugins.Lucene;

/// <summary>
/// Rebuild Index action for Lucene search plugin
/// </summary>
public class RebuildIndexAction : IPluginAction
{
    private readonly LuceneSearchStrategy _strategy;
    private readonly ILogger<RebuildIndexAction> _logger;

    public string Id => "lucene-rebuild-index";
    public string Name => "Rebuild Search Index";
    public string Description => "Clears and rebuilds the entire Lucene search index from all pages in the database. This may take several minutes for large wikis.";
    public string? IconClass => "fas fa-sync-alt";
    public bool RequiresConfirmation => true;
    public string? ConfirmationMessage => "Are you sure you want to rebuild the search index? This will clear the existing index and re-index all pages.";
    public bool IsLongRunning => true;

    public RebuildIndexAction(
        LuceneSearchStrategy strategy,
        ILogger<RebuildIndexAction> logger)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public async Task<PluginActionResult> ExecuteAsync(IServiceProvider serviceProvider, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting Lucene index rebuild");

            // Clear the existing index
            await _strategy.RebuildIndexAsync(cancellationToken);

            // Create a new scope to get fresh repository instances
            using var scope = serviceProvider.CreateScope();
            var pageRepository = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();

            // Get all pages from database
            var pages = await pageRepository.GetAllAsync(cancellationToken);
            var pagesList = pages.ToList();

            _logger.LogInformation("Indexing {Count} pages", pagesList.Count);

            // Create search documents for pages
            var documents = new List<SearchDocument>();
            foreach (var page in pagesList)
            {
                var latestContent = await pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
                if (latestContent == null)
                    continue;

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
                    ModifiedOn = page.ModifiedOn,
                    Metadata = new Dictionary<string, string>
                    {
                        { "DocumentType", "page" }
                    }
                });
            }

            // Get all files from database
            var files = await fileRepository.GetAllAsync(cancellationToken);
            var filesList = files.ToList();

            _logger.LogInformation("Indexing {Count} files", filesList.Count);

            // Create search documents for files
            foreach (var file in filesList)
            {
                documents.Add(new SearchDocument
                {
                    Id = file.Id.ToString(),
                    Title = file.FileName,
                    Slug = file.FileName,
                    Content = file.Description ?? string.Empty,
                    CategoryId = null,
                    CategoryName = file.Folder?.Name,
                    Tags = new List<string>(), // Files don't have tags in the current schema
                    Author = file.UploadedBy,
                    CreatedOn = file.UploadedOn,
                    ModifiedOn = file.UploadedOn, // Files don't have a separate ModifiedOn in current schema
                    Metadata = new Dictionary<string, string>
                    {
                        { "DocumentType", "file" },
                        { "FileId", file.Id.ToString() },
                        { "FileName", file.FileName },
                        { "FileSize", file.FileSize.ToString() },
                        { "ContentType", file.ContentType }
                    }
                });
            }

            // Index all documents
            await _strategy.IndexDocumentsAsync(documents, cancellationToken);

            _logger.LogInformation("Lucene index rebuild completed successfully");

            return PluginActionResult.Successful(
                $"Successfully rebuilt search index with {pagesList.Count} pages and {filesList.Count} files",
                new Dictionary<string, object>
                {
                    { "PagesIndexed", pagesList.Count },
                    { "FilesIndexed", filesList.Count },
                    { "TotalDocuments", documents.Count },
                    { "CompletedAt", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding Lucene index");
            return PluginActionResult.Failed(
                "Failed to rebuild search index",
                ex.Message);
        }
    }
}

/// <summary>
/// Optimize Index action for Lucene search plugin
/// </summary>
public class OptimizeIndexAction : IPluginAction
{
    private readonly LuceneSearchStrategy _strategy;
    private readonly ILogger<OptimizeIndexAction> _logger;

    public string Id => "lucene-optimize-index";
    public string Name => "Optimize Search Index";
    public string Description => "Optimizes the Lucene search index for better performance by merging segments. This may improve search speed.";
    public string? IconClass => "fas fa-tachometer-alt";
    public bool RequiresConfirmation => false;
    public string? ConfirmationMessage => null;
    public bool IsLongRunning => true;

    public OptimizeIndexAction(
        LuceneSearchStrategy strategy,
        ILogger<OptimizeIndexAction> logger)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public async Task<PluginActionResult> ExecuteAsync(IServiceProvider serviceProvider, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting Lucene index optimization");

            await _strategy.OptimizeIndexAsync(cancellationToken);

            _logger.LogInformation("Lucene index optimization completed successfully");

            return PluginActionResult.Successful(
                "Successfully optimized search index",
                new Dictionary<string, object>
                {
                    { "CompletedAt", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing Lucene index");
            return PluginActionResult.Failed(
                "Failed to optimize search index",
                ex.Message);
        }
    }
}

/// <summary>
/// Clear Index action for Lucene search plugin
/// </summary>
public class ClearIndexAction : IPluginAction
{
    private readonly LuceneSearchStrategy _strategy;
    private readonly ILogger<ClearIndexAction> _logger;

    public string Id => "lucene-clear-index";
    public string Name => "Clear Search Index";
    public string Description => "Clears the entire Lucene search index. Use this if you want to start fresh or troubleshoot index issues.";
    public string? IconClass => "fas fa-trash-alt";
    public bool RequiresConfirmation => true;
    public string? ConfirmationMessage => "Are you sure you want to clear the search index? All indexed data will be removed and search will not work until you rebuild the index.";
    public bool IsLongRunning => false;

    public ClearIndexAction(
        LuceneSearchStrategy strategy,
        ILogger<ClearIndexAction> logger)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public async Task<PluginActionResult> ExecuteAsync(IServiceProvider serviceProvider, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("Clearing Lucene search index");

            await _strategy.ClearIndexAsync(cancellationToken);

            _logger.LogInformation("Lucene index cleared successfully");

            return PluginActionResult.Successful(
                "Successfully cleared search index",
                new Dictionary<string, object>
                {
                    { "CompletedAt", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Lucene index");
            return PluginActionResult.Failed(
                "Failed to clear search index",
                ex.Message);
        }
    }
}

/// <summary>
/// Get Index Stats action for Lucene search plugin
/// </summary>
public class GetIndexStatsAction : IPluginAction
{
    private readonly LuceneSearchStrategy _strategy;
    private readonly ILogger<GetIndexStatsAction> _logger;

    public string Id => "lucene-get-stats";
    public string Name => "View Index Statistics";
    public string Description => "Displays statistics about the Lucene search index including document count, size, and last update time.";
    public string? IconClass => "fas fa-chart-bar";
    public bool RequiresConfirmation => false;
    public string? ConfirmationMessage => null;
    public bool IsLongRunning => false;

    public GetIndexStatsAction(
        LuceneSearchStrategy strategy,
        ILogger<GetIndexStatsAction> logger)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public async Task<PluginActionResult> ExecuteAsync(IServiceProvider serviceProvider, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _strategy.GetIndexStatsAsync(cancellationToken);

            var sizeInMB = stats.IndexSizeBytes / (1024.0 * 1024.0);

            return PluginActionResult.Successful(
                $"Index contains {stats.TotalDocuments} documents ({sizeInMB:F2} MB)",
                new Dictionary<string, object>
                {
                    { "TotalDocuments", stats.TotalDocuments },
                    { "IndexSizeBytes", stats.IndexSizeBytes },
                    { "IndexSizeMB", sizeInMB },
                    { "LastIndexed", stats.LastIndexed ?? DateTime.MinValue },
                    { "IsValid", stats.IsValid }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Lucene index stats");
            return PluginActionResult.Failed(
                "Failed to get index statistics",
                ex.Message);
        }
    }
}
