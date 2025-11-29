using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Services.Caching;
using System.Text.RegularExpressions;

namespace Squirrel.Wiki.Core.Services.Search;

/// <summary>
/// Service implementation for search operations
/// </summary>
/// <remarks>
/// This is a database-based search implementation. For production use with large datasets,
/// consider integrating a full-text search engine like Elasticsearch or Azure Cognitive Search.
/// </remarks>
public class SearchService : BaseService, ISearchService
{
    private readonly IPageRepository _pageRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IAuthorizationService _authorizationService;
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);

    public SearchService(
        IPageRepository pageRepository,
        IFileRepository fileRepository,
        IFolderRepository folderRepository,
        IAuthorizationService authorizationService,
        ILogger<SearchService> logger,
        ICacheService cache,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cache, eventPublisher, null, configuration)
    {
        _pageRepository = pageRepository;
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _authorizationService = authorizationService;
    }

    public async Task<SearchResultsDto> SearchAsync(string searchTerm, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new SearchResultsDto
            {
                Query = searchTerm,
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = 0
            };
        }

        LogInfo("Searching for: {SearchTerm}", searchTerm);

        // Search in pages
        var pages = await _pageRepository.SearchAsync(searchTerm, cancellationToken);
        
        // Convert to search results
        var results = new List<SearchResultItemDto>();
        foreach (var page in pages)
        {
            var latestContent = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            if (latestContent != null)
            {
                var result = new SearchResultItemDto
                {
                    PageId = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    Excerpt = GenerateContentSummary(latestContent.Text, searchTerm),
                    ModifiedBy = page.ModifiedBy ?? page.CreatedBy,
                    ModifiedOn = page.ModifiedOn,
                    Score = (float)CalculateRelevance(page.Title, latestContent.Text, searchTerm)
                };
                results.Add(result);
            }
        }

        // Sort by relevance
        results = results.OrderByDescending(r => r.Score).ToList();

        // Apply pagination
        var totalResults = results.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
        var paginatedResults = results
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResultsDto
        {
            Query = searchTerm,
            Results = paginatedResults,
            TotalResults = totalResults,
            Page = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<SearchResultsDto> SearchByTagAsync(string tag, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return new SearchResultsDto
            {
                Query = tag,
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = 0
            };
        }

        LogInfo("Searching by tag: {Tag}", tag);

        var pages = await _pageRepository.GetByTagAsync(tag, cancellationToken);
        
        var results = new List<SearchResultItemDto>();
        foreach (var page in pages)
        {
            var latestContent = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            if (latestContent != null)
            {
                var result = new SearchResultItemDto
                {
                    PageId = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    Excerpt = GenerateContentSummary(latestContent.Text, string.Empty, 200),
                    ModifiedBy = page.ModifiedBy ?? page.CreatedBy,
                    ModifiedOn = page.ModifiedOn,
                    Score = 1.0f // All results are equally relevant for tag search
                };
                results.Add(result);
            }
        }

        // Sort by modified date
        results = results.OrderByDescending(r => r.ModifiedOn).ToList();

        // Apply pagination
        var totalResults = results.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
        var paginatedResults = results
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResultsDto
        {
            Query = tag,
            Results = paginatedResults,
            TotalResults = totalResults,
            Page = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<SearchResultsDto> SearchByCategoryAsync(int categoryId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        LogInfo("Searching by category: {CategoryId}", categoryId);

        var pages = await _pageRepository.GetByCategoryAsync(categoryId, cancellationToken);
        
        var results = new List<SearchResultItemDto>();
        foreach (var page in pages)
        {
            var latestContent = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            if (latestContent != null)
            {
                var result = new SearchResultItemDto
                {
                    PageId = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    Excerpt = GenerateContentSummary(latestContent.Text, string.Empty, 200),
                    ModifiedBy = page.ModifiedBy ?? page.CreatedBy,
                    ModifiedOn = page.ModifiedOn,
                    Score = 1.0f
                };
                results.Add(result);
            }
        }

        // Sort by title
        results = results.OrderBy(r => r.Title).ToList();

        // Apply pagination
        var totalResults = results.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
        var paginatedResults = results
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResultsDto
        {
            Query = $"Category:{categoryId}",
            Results = paginatedResults,
            TotalResults = totalResults,
            Page = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<SearchResultsDto> SearchByAuthorAsync(string author, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return new SearchResultsDto
            {
                Query = author,
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = 0
            };
        }

        LogInfo("Searching by author: {Author}", author);

        var pages = await _pageRepository.GetByAuthorAsync(author, cancellationToken);
        
        var results = new List<SearchResultItemDto>();
        foreach (var page in pages)
        {
            var latestContent = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            if (latestContent != null)
            {
                var result = new SearchResultItemDto
                {
                    PageId = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    Excerpt = GenerateContentSummary(latestContent.Text, string.Empty, 200),
                    ModifiedBy = page.ModifiedBy ?? page.CreatedBy,
                    ModifiedOn = page.ModifiedOn,
                    Score = 1.0f
                };
                results.Add(result);
            }
        }

        // Sort by modified date
        results = results.OrderByDescending(r => r.ModifiedOn).ToList();

        // Apply pagination
        var totalResults = results.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
        var paginatedResults = results
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResultsDto
        {
            Query = author,
            Results = paginatedResults,
            TotalResults = totalResults,
            Page = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<SearchResultsDto> AdvancedSearchAsync(SearchQueryDto searchQuery, CancellationToken cancellationToken = default)
    {
        // Start with basic search
        var results = await SearchAsync(searchQuery.Query ?? string.Empty, searchQuery.Page, searchQuery.PageSize, cancellationToken);

        // Apply additional filters if specified
        if (searchQuery.CategoryId.HasValue)
        {
            results = await SearchByCategoryAsync(searchQuery.CategoryId.Value, searchQuery.Page, searchQuery.PageSize, cancellationToken);
        }

        if (searchQuery.Tags != null && searchQuery.Tags.Any())
        {
            results = await SearchByTagsAsync(searchQuery.Tags, searchQuery.Page, searchQuery.PageSize, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery.Author))
        {
            results = await SearchByAuthorAsync(searchQuery.Author, searchQuery.Page, searchQuery.PageSize, cancellationToken);
        }

        if (searchQuery.StartDate.HasValue || searchQuery.EndDate.HasValue)
        {
            results = await SearchByDateRangeAsync(searchQuery.StartDate, searchQuery.EndDate, searchQuery.Page, searchQuery.PageSize, cancellationToken);
        }

        return results;
    }

    public async Task<SearchResultsDto> SearchInCategoryAsync(string query, int categoryId, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var categoryResults = await SearchByCategoryAsync(categoryId, pageNum, pageSize, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(query))
        {
            return categoryResults;
        }

        // Filter category results by query
        var filteredResults = categoryResults.Results
            .Where(r => r.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                       r.Excerpt.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new SearchResultsDto
        {
            Query = query,
            Results = filteredResults,
            TotalResults = filteredResults.Count,
            Page = pageNum,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(filteredResults.Count / (double)pageSize)
        };
    }

    public async Task<SearchResultsDto> SearchByTagsAsync(List<string> tags, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (tags == null || !tags.Any())
        {
            return new SearchResultsDto
            {
                Query = string.Join(", ", tags ?? new List<string>()),
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNum,
                PageSize = pageSize,
                TotalPages = 0
            };
        }

        // For simplicity, search by first tag (can be enhanced to support multiple tags)
        return await SearchByTagAsync(tags.First(), pageNum, pageSize, cancellationToken);
    }

    public async Task<SearchResultsDto> SearchByDateRangeAsync(DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var allPages = await _pageRepository.GetAllAsync(cancellationToken);
        
        // Filter by date range
        var filteredPages = allPages.Where(p =>
        {
            if (startDate.HasValue && p.ModifiedOn < startDate.Value)
                return false;
            if (endDate.HasValue && p.ModifiedOn > endDate.Value)
                return false;
            return true;
        }).ToList();

        var results = new List<SearchResultItemDto>();
        foreach (var pg in filteredPages)
        {
            var latestContent = await _pageRepository.GetLatestContentAsync(pg.Id, cancellationToken);
            if (latestContent != null)
            {
                results.Add(new SearchResultItemDto
                {
                    PageId = pg.Id,
                    Title = pg.Title,
                    Slug = pg.Slug,
                    Excerpt = GenerateContentSummary(latestContent.Text, string.Empty, 200),
                    ModifiedBy = pg.ModifiedBy ?? pg.CreatedBy,
                    ModifiedOn = pg.ModifiedOn,
                    Score = 1.0f
                });
            }
        }

        results = results.OrderByDescending(r => r.ModifiedOn).ToList();

        var totalResults = results.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
        var paginatedResults = results
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResultsDto
        {
            Query = $"Date Range: {startDate?.ToShortDateString()} - {endDate?.ToShortDateString()}",
            Results = paginatedResults,
            TotalResults = totalResults,
            Page = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        return await GetSearchSuggestionsAsync(partialQuery, maxSuggestions, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetSearchSuggestionsAsync(string partialTerm, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partialTerm) || partialTerm.Length < 2)
        {
            return Enumerable.Empty<string>();
        }

        LogDebug("Getting search suggestions for: {PartialTerm}", partialTerm);

        // Get pages that match the partial term
        var pages = await _pageRepository.SearchAsync(partialTerm, cancellationToken);
        
        // Extract unique titles that start with or contain the search term
        var suggestions = pages
            .Select(p => p.Title)
            .Where(title => title.Contains(partialTerm, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(title => title.StartsWith(partialTerm, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(title => title)
            .Take(maxSuggestions)
            .ToList();

        return suggestions;
    }

    /// <summary>
    /// Generates a content summary with the search term highlighted in context
    /// </summary>
    private string GenerateContentSummary(string content, string searchTerm, int maxLength = 200)
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

        var summary = content.Substring(start, end - start);
        
        // Add ellipsis
        if (start > 0)
        {
            summary = "..." + summary;
        }
        if (end < content.Length)
        {
            summary = summary + "...";
        }

        return summary;
    }

    /// <summary>
    /// Calculates relevance score based on search term occurrence in title and content
    /// </summary>
    private double CalculateRelevance(string title, string content, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return 0;
        }

        double score = 0;

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
        
        score += occurrences * 0.5;

        return score;
    }

    // Index management methods (stubs for database-based search)
    public async Task IndexPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        // In a database-based search, indexing is automatic
        // This method is a no-op but kept for interface compatibility
        LogDebug("IndexPageAsync called for page {PageId} (no-op for database search)", pageId);
        await Task.CompletedTask;
    }

    public async Task IndexPagesAsync(IEnumerable<int> pageIds, CancellationToken cancellationToken = default)
    {
        // In a database-based search, indexing is automatic
        LogDebug("IndexPagesAsync called for {Count} pages (no-op for database search)", pageIds.Count());
        await Task.CompletedTask;
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        // In a database-based search, there's no separate index to rebuild
        LogInfo("RebuildIndexAsync called (no-op for database search)");
        await Task.CompletedTask;
    }

    public async Task RemoveFromIndexAsync(int pageId, CancellationToken cancellationToken = default)
    {
        // In a database-based search, removal is automatic when page is deleted
        LogDebug("RemoveFromIndexAsync called for page {PageId} (no-op for database search)", pageId);
        await Task.CompletedTask;
    }

    public async Task OptimizeIndexAsync(CancellationToken cancellationToken = default)
    {
        // In a database-based search, optimization is handled by the database
        LogDebug("OptimizeIndexAsync called (no-op for database search)");
        await Task.CompletedTask;
    }

    public async Task<SearchIndexStatsDto> GetIndexStatsAsync(CancellationToken cancellationToken = default)
    {
        var allPages = await _pageRepository.GetAllAsync(cancellationToken);
        var totalPages = allPages.Count();

        return new SearchIndexStatsDto
        {
            TotalDocuments = totalPages,
            IndexSizeBytes = 0, // Not applicable for database search
            LastOptimized = DateTime.UtcNow,
            IsValid = true
        };
    }

    public async Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        // In a database-based search, there's no separate index to clear
        LogWarning("ClearIndexAsync called (no-op for database search)");
        await Task.CompletedTask;
    }

    public async Task<bool> IsIndexValidAsync(CancellationToken cancellationToken = default)
    {
        // Database search is always valid if database is accessible
        try
        {
            await _pageRepository.GetAllAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<PageDto>> GetSimilarPagesAsync(int pageId, int count = 5, CancellationToken cancellationToken = default)
    {
        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null)
        {
            return Enumerable.Empty<PageDto>();
        }

        // Get pages in the same category (if page has a category)
        if (!page.CategoryId.HasValue)
        {
            return Enumerable.Empty<PageDto>();
        }

        var similarPages = await _pageRepository.GetByCategoryAsync(page.CategoryId.Value, cancellationToken);
        
        // Exclude the current page and limit results
        var results = similarPages
            .Where(p => p.Id != pageId)
            .Take(count)
            .Select(p => new PageDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                CategoryId = p.CategoryId,
                CreatedBy = p.CreatedBy,
                CreatedOn = p.CreatedOn,
                ModifiedBy = p.ModifiedBy,
                ModifiedOn = p.ModifiedOn,
                IsLocked = p.IsLocked,
                IsDeleted = p.IsDeleted
            });

        return results;
    }

    public async Task<SearchResultsDto> FuzzySearchAsync(string query, float minSimilarity = 0.7f, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // For database-based search, fuzzy search is approximated with regular search
        // A full implementation would use Levenshtein distance or similar algorithms
        LogDebug("FuzzySearchAsync called with query '{Query}' (using regular search)", query);
        
        return await SearchAsync(query, pageNum, pageSize, cancellationToken);
    }

    // File search methods
    public async Task IndexFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        LogDebug("IndexFileAsync called for file {FileId} - publishing FileIndexRequestedEvent", fileId);
        
        // Publish event so search plugins can index the file
        await EventPublisher.PublishAsync(new Events.Search.FileIndexRequestedEvent(fileId), cancellationToken);
    }

    public async Task IndexFilesAsync(IEnumerable<Guid> fileIds, CancellationToken cancellationToken = default)
    {
        // In a database-based search, indexing is automatic
        LogDebug("IndexFilesAsync called for {Count} files (no-op for database search)", fileIds.Count());
        await Task.CompletedTask;
    }

    public async Task RemoveFileFromIndexAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        // In a database-based search, removal is automatic when file is deleted
        LogDebug("RemoveFileFromIndexAsync called for file {FileId} (no-op for database search)", fileId);
        await Task.CompletedTask;
    }

    public async Task<SearchResultsDto> SearchFilesAsync(string query, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResultsDto
            {
                Query = query,
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = 0
            };
        }

        LogInfo("Searching files for: {Query}", query);

        // Search in files using database
        var files = await _fileRepository.SearchAsync(query, cancellationToken);
        
        // Filter by permissions
        var accessibleFiles = new List<Core.Database.Entities.File>();
        foreach (var file in files)
        {
            if (await _authorizationService.CanViewFileAsync(file, cancellationToken))
            {
                accessibleFiles.Add(file);
            }
        }

        // Convert to search results
        var results = new List<SearchResultItemDto>();
        foreach (var file in accessibleFiles)
        {
            var folder = file.FolderId.HasValue 
                ? await _folderRepository.GetByIdAsync(file.FolderId.Value, cancellationToken)
                : null;

            var result = new SearchResultItemDto
            {
                Type = SearchResultType.File,
                FileId = file.Id,
                Title = file.FileName,
                Slug = file.FileName,
                Excerpt = file.Description ?? string.Empty,
                ModifiedBy = file.UploadedBy,
                ModifiedOn = file.UploadedOn,
                Score = (float)CalculateFileRelevance(file.FileName, file.Description ?? string.Empty, query),
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                FolderPath = folder?.Name,
                DownloadUrl = $"/files/download/{file.Id}"
            };
            results.Add(result);
        }

        // Sort by relevance
        results = results.OrderByDescending(r => r.Score).ToList();

        // Apply pagination
        var totalResults = results.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
        var paginatedResults = results
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResultsDto
        {
            Query = query,
            Results = paginatedResults,
            TotalResults = totalResults,
            Page = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<SearchResultsDto> SearchAllAsync(string query, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResultsDto
            {
                Query = query,
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = 0
            };
        }

        LogInfo("Searching all content for: {Query}", query);

        // Search both pages and files
        var pageResults = await SearchAsync(query, 1, int.MaxValue, cancellationToken);
        var fileResults = await SearchFilesAsync(query, 1, int.MaxValue, cancellationToken);

        // Combine results
        var allResults = pageResults.Results.Concat(fileResults.Results).ToList();

        // Sort by relevance
        allResults = allResults.OrderByDescending(r => r.Score).ToList();

        // Apply pagination
        var totalResults = allResults.Count;
        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
        var paginatedResults = allResults
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResultsDto
        {
            Query = query,
            Results = paginatedResults,
            TotalResults = totalResults,
            Page = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    /// <summary>
    /// Calculates relevance score for file search results
    /// </summary>
    private double CalculateFileRelevance(string fileName, string description, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return 0;
        }

        double score = 0;

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
