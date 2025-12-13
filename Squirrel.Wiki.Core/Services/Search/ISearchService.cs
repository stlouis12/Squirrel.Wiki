using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Services.Search;

/// <summary>
/// Service interface for search operations using Lucene.NET
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Performs a full-text search across all pages
    /// </summary>
    Task<SearchResultsDto> SearchAsync(string searchTerm, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an advanced search with filters
    /// </summary>
    Task<SearchResultsDto> AdvancedSearchAsync(SearchQueryDto searchQuery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches within a specific category
    /// </summary>
    Task<SearchResultsDto> SearchInCategoryAsync(string query, int categoryId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for pages with specific tags
    /// </summary>
    Task<SearchResultsDto> SearchByTagsAsync(List<string> tags, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for pages by a specific author
    /// </summary>
    Task<SearchResultsDto> SearchByAuthorAsync(string author, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for pages modified within a date range
    /// </summary>
    Task<SearchResultsDto> SearchByDateRangeAsync(DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search suggestions based on partial query
    /// </summary>
    Task<IEnumerable<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a single page
    /// </summary>
    Task IndexPageAsync(int pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple pages
    /// </summary>
    Task IndexPagesAsync(IEnumerable<int> pageIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the entire search index
    /// </summary>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a page from the search index
    /// </summary>
    Task RemoveFromIndexAsync(int pageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes the search index for better performance
    /// </summary>
    Task OptimizeIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search index statistics
    /// </summary>
    Task<SearchIndexStatsDto> GetIndexStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entire search index
    /// </summary>
    Task ClearIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the search index exists and is valid
    /// </summary>
    Task<bool> IsIndexValidAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets similar pages based on content similarity
    /// </summary>
    Task<IEnumerable<PageDto>> GetSimilarPagesAsync(int pageId, int count = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a fuzzy search (tolerates typos)
    /// </summary>
    Task<SearchResultsDto> FuzzySearchAsync(string query, float minSimilarity = 0.7f, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a single file
    /// </summary>
    Task IndexFileAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple files
    /// </summary>
    Task IndexFilesAsync(IEnumerable<Guid> fileIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a file from the search index
    /// </summary>
    Task RemoveFileFromIndexAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for files
    /// </summary>
    Task<SearchResultsDto> SearchFilesAsync(string query, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for both pages and files
    /// </summary>
    Task<SearchResultsDto> SearchAllAsync(string query, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}
