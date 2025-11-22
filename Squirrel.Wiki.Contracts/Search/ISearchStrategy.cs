namespace Squirrel.Wiki.Contracts.Search;

/// <summary>
/// Defines the contract for search strategy implementations.
/// Different search engines (Lucene, Elasticsearch, Azure Search, etc.) can implement this interface.
/// </summary>
public interface ISearchStrategy
{
    /// <summary>
    /// Gets the name of the search strategy (e.g., "Lucene", "Elasticsearch", "AzureSearch")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of the search strategy implementation
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets whether this search strategy is currently available and configured
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the search strategy with configuration
    /// </summary>
    Task InitializeAsync(SearchConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a search with the given request
    /// </summary>
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a document in the search engine
    /// </summary>
    Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple documents in the search engine
    /// </summary>
    Task IndexDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the search index
    /// </summary>
    Task RemoveDocumentAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple documents from the search index
    /// </summary>
    Task RemoveDocumentsAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the entire search index
    /// </summary>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes the search index for better performance
    /// </summary>
    Task OptimizeIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entire search index
    /// </summary>
    Task ClearIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the search index
    /// </summary>
    Task<SearchIndexStats> GetIndexStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search suggestions based on partial query
    /// </summary>
    Task<IEnumerable<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds similar documents based on content similarity
    /// </summary>
    Task<IEnumerable<SearchResult>> FindSimilarAsync(string documentId, int count = 5, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for a search strategy
/// </summary>
public class SearchConfiguration
{
    /// <summary>
    /// Path or connection string for the search index
    /// </summary>
    public string IndexPath { get; set; } = string.Empty;

    /// <summary>
    /// Additional configuration options specific to the search strategy
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new();
}

/// <summary>
/// Represents a search request
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// The search query text
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Page number for pagination (1-based)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of results per page
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Filter by category IDs
    /// </summary>
    public List<int>? CategoryIds { get; set; }

    /// <summary>
    /// Filter by tags
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Filter by author
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Filter by start date
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Filter by end date
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Whether to include content in results
    /// </summary>
    public bool IncludeContent { get; set; } = false;

    /// <summary>
    /// Minimum similarity for fuzzy search (0.0 to 1.0)
    /// </summary>
    public float? MinSimilarity { get; set; }

    /// <summary>
    /// Fields to search in (null = all fields)
    /// </summary>
    public List<string>? SearchFields { get; set; }

    /// <summary>
    /// Fields to boost in search results
    /// </summary>
    public Dictionary<string, float>? FieldBoosts { get; set; }
}

/// <summary>
/// Represents a search response
/// </summary>
public class SearchResponse
{
    /// <summary>
    /// The original query
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Total number of results found
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of results per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Search results for the current page
    /// </summary>
    public List<SearchResult> Results { get; set; } = new();

    /// <summary>
    /// Facets for filtering (e.g., category counts, tag counts)
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> Facets { get; set; } = new();

    /// <summary>
    /// Time taken to execute the search (in milliseconds)
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Represents a single search result
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Document ID (typically the page ID)
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Document title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document slug/URL
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Excerpt or snippet showing matched content
    /// </summary>
    public string Excerpt { get; set; } = string.Empty;

    /// <summary>
    /// Full content (if requested)
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Relevance score (higher is more relevant)
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Category ID
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Category name
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Tags associated with the document
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Author of the document
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// When the document was created
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// When the document was last modified
    /// </summary>
    public DateTime ModifiedOn { get; set; }

    /// <summary>
    /// Highlighted fragments showing where matches occurred
    /// </summary>
    public Dictionary<string, List<string>> Highlights { get; set; } = new();
}

/// <summary>
/// Represents a document to be indexed
/// </summary>
public class SearchDocument
{
    /// <summary>
    /// Unique document ID (typically the page ID)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document slug/URL
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Document content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Category ID
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Category name
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// Tags associated with the document
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Author of the document
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// When the document was created
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// When the document was last modified
    /// </summary>
    public DateTime ModifiedOn { get; set; }

    /// <summary>
    /// Additional metadata fields
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Statistics about the search index
/// </summary>
public class SearchIndexStats
{
    /// <summary>
    /// Total number of documents in the index
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Size of the index in bytes
    /// </summary>
    public long IndexSizeBytes { get; set; }

    /// <summary>
    /// When the index was last optimized
    /// </summary>
    public DateTime? LastOptimized { get; set; }

    /// <summary>
    /// When the index was last updated
    /// </summary>
    public DateTime? LastIndexed { get; set; }

    /// <summary>
    /// Whether the index is valid and accessible
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Additional strategy-specific statistics
    /// </summary>
    public Dictionary<string, object> AdditionalStats { get; set; } = new();
}
