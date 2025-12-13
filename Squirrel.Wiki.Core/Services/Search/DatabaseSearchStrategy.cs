using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Search;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Security;
using System.Text.RegularExpressions;

namespace Squirrel.Wiki.Core.Services.Search;

/// <summary>
/// Basic database-based search strategy that queries page titles and content directly
/// </summary>
public class DatabaseSearchStrategy : ISearchStrategy
{
    private readonly IPageRepository _pageRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<DatabaseSearchStrategy> _logger;
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);

    public string Name => "Database";
    public string Version => "1.0.0";

    public DatabaseSearchStrategy(
        IPageRepository pageRepository,
        IFileRepository fileRepository,
        IFolderRepository folderRepository,
        IAuthorizationService authorizationService,
        ILogger<DatabaseSearchStrategy> logger)
    {
        _pageRepository = pageRepository;
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Database search is always available
        return Task.FromResult(true);
    }

    public Task InitializeAsync(SearchConfiguration configuration, CancellationToken cancellationToken = default)
    {
        // No initialization needed for database search
        _logger.LogInformation("Database search strategy initialized");
        return Task.CompletedTask;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new SearchResponse
            {
                Query = request.Query,
                TotalResults = 0,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = 0,
                Results = new List<SearchResult>(),
                ExecutionTimeMs = 0
            };
        }

        _logger.LogDebug("Database search for: {Query}, DocumentTypes: {DocumentTypes}", 
            request.Query, 
            request.DocumentTypes != null ? string.Join(", ", request.DocumentTypes) : "all");

        var results = new List<SearchResult>();

        // Determine what to search based on DocumentTypes filter
        var searchPages = request.DocumentTypes == null || 
                         !request.DocumentTypes.Any() || 
                         request.DocumentTypes.Contains("page", StringComparer.OrdinalIgnoreCase);
        
        var searchFiles = request.DocumentTypes == null || 
                         !request.DocumentTypes.Any() || 
                         request.DocumentTypes.Contains("file", StringComparer.OrdinalIgnoreCase);

        // Search in pages if requested
        if (searchPages)
        {
            var pageResults = await SearchPagesAsync(request, cancellationToken);
            results.AddRange(pageResults);
        }

        // Search in files if requested
        if (searchFiles)
        {
            var fileResults = await SearchFilesAsync(request, cancellationToken);
            results.AddRange(fileResults);
        }

        // Sort by relevance
        results = results.OrderByDescending(r => r.Score).ToList();

        // Apply pagination
        var totalResults = results.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)request.PageSize);
        var paginatedResults = results
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

        return new SearchResponse
        {
            Query = request.Query,
            TotalResults = totalResults,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages,
            Results = paginatedResults,
            ExecutionTimeMs = executionTime
        };
    }

    private async Task<List<SearchResult>> SearchPagesAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var pages = await _pageRepository.SearchAsync(request.Query, cancellationToken);
        var filteredPages = ApplyPageFilters(pages, request);

        var results = new List<SearchResult>();
        foreach (var page in filteredPages)
        {
            var result = await CreatePageSearchResultAsync(page, request, cancellationToken);
            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private static IEnumerable<Database.Entities.Page> ApplyPageFilters(IEnumerable<Database.Entities.Page> pages, SearchRequest request)
    {
        if (request.CategoryIds != null && request.CategoryIds.Any())
        {
            pages = pages.Where(p => p.CategoryId.HasValue && request.CategoryIds.Contains(p.CategoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.Author))
        {
            pages = pages.Where(p => 
                p.CreatedBy.Contains(request.Author, StringComparison.OrdinalIgnoreCase) ||
                (p.ModifiedBy != null && p.ModifiedBy.Contains(request.Author, StringComparison.OrdinalIgnoreCase)));
        }

        if (request.StartDate.HasValue)
        {
            pages = pages.Where(p => p.ModifiedOn >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            pages = pages.Where(p => p.ModifiedOn <= request.EndDate.Value);
        }

        return pages;
    }

    private async Task<SearchResult?> CreatePageSearchResultAsync(
        Database.Entities.Page page, 
        SearchRequest request, 
        CancellationToken cancellationToken)
    {
        var latestContent = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
        if (latestContent == null)
        {
            return null;
        }

        var contentText = latestContent.Text ?? string.Empty;
        
        if (!PassesTagFilter(page, request.Tags))
        {
            return null;
        }

        var score = CalculateRelevance(page.Title, contentText, request.Query);

        return new SearchResult
        {
            DocumentId = page.Id.ToString(),
            Title = page.Title,
            Slug = page.Slug,
            Excerpt = GenerateExcerpt(contentText, request.Query),
            Content = request.IncludeContent ? contentText : null,
            Score = score,
            CategoryId = page.CategoryId,
            CategoryName = page.Category?.Name,
            Tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>(),
            Author = page.ModifiedBy ?? page.CreatedBy,
            CreatedOn = page.CreatedOn,
            ModifiedOn = page.ModifiedOn
        };
    }

    private static bool PassesTagFilter(Database.Entities.Page page, IEnumerable<string>? requestedTags)
    {
        if (requestedTags == null || !requestedTags.Any())
        {
            return true;
        }

        var pageTags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>();
        return requestedTags.Any(tag => pageTags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<List<SearchResult>> SearchFilesAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var files = await _fileRepository.SearchAsync(request.Query, cancellationToken);
        
        // Filter files by permissions - batch authorization check
        var viewPermissions = await _authorizationService.CanViewFilesAsync(files);
        var authorizedFiles = files.Where(f => viewPermissions.GetValueOrDefault(f.Id, false)).ToList();
        
        _logger.LogDebug("File search filtered by permissions: {AuthorizedCount}/{TotalCount} files visible", 
            authorizedFiles.Count, files.Count());
        
        var results = new List<SearchResult>();
        foreach (var file in authorizedFiles)
        {
            var folder = file.FolderId.HasValue 
                ? await _folderRepository.GetByIdAsync(file.FolderId.Value, cancellationToken)
                : null;

            var folderPath = folder != null 
                ? await _folderRepository.GetFolderPathAsync(folder.Id, cancellationToken) ?? "/" 
                : "/";

            var score = CalculateFileRelevance(file.FileName, file.Description ?? string.Empty, request.Query);

            var result = new SearchResult
            {
                DocumentId = file.Id.ToString(),
                Title = file.FileName,
                Slug = string.Empty,
                Excerpt = file.Description ?? string.Empty,
                Content = null,
                Score = score,
                Author = file.UploadedBy,
                CreatedOn = file.UploadedOn,
                ModifiedOn = file.UploadedOn,
                Highlights = new Dictionary<string, List<string>>
                {
                    { "DocumentType", new List<string> { "file" } },
                    { "FileName", new List<string> { file.FileName } },
                    { "ContentType", new List<string> { file.ContentType } },
                    { "FileSize", new List<string> { file.FileSize.ToString() } },
                    { "FolderPath", new List<string> { folderPath } }
                }
            };

            if (file.FolderId.HasValue)
            {
                result.Highlights["FolderId"] = new List<string> { file.FolderId.Value.ToString() };
            }

            results.Add(result);
        }

        return results;
    }

    public Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        // Database search doesn't require separate indexing
        _logger.LogDebug("IndexDocumentAsync called for document {DocumentId} (no-op for database search)", document.Id);
        return Task.CompletedTask;
    }

    public Task IndexDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default)
    {
        // Database search doesn't require separate indexing
        _logger.LogDebug("IndexDocumentsAsync called for {Count} documents (no-op for database search)", documents.Count());
        return Task.CompletedTask;
    }

    public Task RemoveDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        // Database search doesn't require separate index management
        _logger.LogDebug("RemoveDocumentAsync called for document {DocumentId} (no-op for database search)", documentId);
        return Task.CompletedTask;
    }

    public Task RemoveDocumentsAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        // Database search doesn't require separate index management
        _logger.LogDebug("RemoveDocumentsAsync called for {Count} documents (no-op for database search)", documentIds.Count());
        return Task.CompletedTask;
    }

    public Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        // Database search doesn't have a separate index to rebuild
        _logger.LogInformation("RebuildIndexAsync called (no-op for database search)");
        return Task.CompletedTask;
    }

    public Task OptimizeIndexAsync(CancellationToken cancellationToken = default)
    {
        // Database search optimization is handled by the database itself
        _logger.LogDebug("OptimizeIndexAsync called (no-op for database search)");
        return Task.CompletedTask;
    }

    public Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        // Database search doesn't have a separate index to clear
        _logger.LogWarning("ClearIndexAsync called (no-op for database search)");
        return Task.CompletedTask;
    }

    public async Task<SearchIndexStats> GetIndexStatsAsync(CancellationToken cancellationToken = default)
    {
        var allPages = await _pageRepository.GetAllAsync(cancellationToken);
        var totalPages = allPages.Count();

        return new SearchIndexStats
        {
            TotalDocuments = totalPages,
            IndexSizeBytes = 0, // Not applicable for database search
            LastOptimized = null,
            LastIndexed = DateTime.UtcNow,
            IsValid = true,
            AdditionalStats = new Dictionary<string, object>
            {
                { "SearchType", "Database" },
                { "RequiresIndexing", false }
            }
        };
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partialQuery) || partialQuery.Length < 2)
        {
            return Enumerable.Empty<string>();
        }

        _logger.LogDebug("Getting search suggestions for: {PartialQuery}", partialQuery);

        var pages = await _pageRepository.SearchAsync(partialQuery, cancellationToken);
        
        var suggestions = pages
            .Select(p => p.Title)
            .Where(title => title.Contains(partialQuery, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(title => title.StartsWith(partialQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(title => title)
            .Take(maxSuggestions)
            .ToList();

        return suggestions;
    }

    public async Task<IEnumerable<SearchResult>> FindSimilarAsync(string documentId, int count = 5, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(documentId, out var pageId))
        {
            return Enumerable.Empty<SearchResult>();
        }

        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null || !page.CategoryId.HasValue)
        {
            return Enumerable.Empty<SearchResult>();
        }

        var similarPages = await _pageRepository.GetByCategoryAsync(page.CategoryId.Value, cancellationToken);
        
        var results = new List<SearchResult>();
        foreach (var similarPage in similarPages.Where(p => p.Id != pageId).Take(count))
        {
            var latestContent = await _pageRepository.GetLatestContentAsync(similarPage.Id, cancellationToken);
            if (latestContent != null)
            {
                results.Add(new SearchResult
                {
                    DocumentId = similarPage.Id.ToString(),
                    Title = similarPage.Title,
                    Slug = similarPage.Slug,
                    Excerpt = GenerateExcerpt(latestContent.Text ?? string.Empty, string.Empty, 200),
                    Score = 1.0f,
                    CategoryId = similarPage.CategoryId,
                    CategoryName = similarPage.Category?.Name,
                    Tags = similarPage.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>(),
                    Author = similarPage.ModifiedBy ?? similarPage.CreatedBy,
                    CreatedOn = similarPage.CreatedOn,
                    ModifiedOn = similarPage.ModifiedOn
                });
            }
        }

        return results;
    }

    private static string GenerateExcerpt(string content, string searchTerm, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        // Remove HTML tags if present
        content = HtmlTagRegex.Replace(content, " ");
        
        // Remove extra whitespace
        content = Regex.Replace(content, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            // No search term, just return the beginning
            return content.Length <= maxLength 
                ? content 
                : content.Substring(0, maxLength) + "...";
        }

        // Find the search term in the content
        var index = content.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
        
        if (index == -1)
        {
            // Search term not found in content, return beginning
            return content.Length <= maxLength 
                ? content 
                : content.Substring(0, maxLength) + "...";
        }

        // Calculate start position to center the search term
        var halfLength = maxLength / 2;
        var start = Math.Max(0, index - halfLength);
        var end = Math.Min(content.Length, start + maxLength);
        
        // Adjust start if we're at the end
        if (end == content.Length && content.Length > maxLength)
        {
            start = Math.Max(0, end - maxLength);
        }

        var excerpt = content.Substring(start, end - start);
        
        // Add ellipsis
        if (start > 0)
        {
            excerpt = "..." + excerpt;
        }
        if (end < content.Length)
        {
            excerpt = excerpt + "...";
        }

        return excerpt;
    }

    private static float CalculateRelevance(string title, string content, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return 0;
        }

        float score = 0;

        // Title matches are worth more
        if (title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            
            // Exact title match is worth even more
            if (title.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }
            // Title starts with search term
            else if (title.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }

        // Count occurrences in content
        var contentLower = content.ToLowerInvariant();
        var searchTermLower = searchTerm.ToLowerInvariant();
        var occurrences = Regex.Matches(contentLower, Regex.Escape(searchTermLower)).Count;
        
        score += occurrences * 0.5f;

        return score;
    }

    private static float CalculateFileRelevance(string fileName, string description, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return 0;
        }

        float score = 0;

        // Filename matches are worth more
        if (fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            
            // Exact filename match is worth even more
            if (fileName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }
            // Filename starts with search term
            else if (fileName.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }

        // Description matches
        if (!string.IsNullOrWhiteSpace(description) && description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }
}
