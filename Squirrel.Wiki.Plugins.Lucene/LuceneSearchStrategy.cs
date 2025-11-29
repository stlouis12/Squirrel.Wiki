using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Search;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System.Text.RegularExpressions;
using LuceneDirectory = Lucene.Net.Store.Directory;
using IODirectory = System.IO.Directory;

namespace Squirrel.Wiki.Plugins.Lucene;

/// <summary>
/// Lucene.NET-based search strategy implementation for high-performance full-text search
/// </summary>
public class LuceneSearchStrategy : ISearchStrategy
{
    private readonly ILogger<LuceneSearchStrategy> _logger;
    private string _indexPath = string.Empty;
    private static readonly LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);

    public string Name => "Lucene.NET";
    public string Version => "4.8.0";

    public LuceneSearchStrategy(ILogger<LuceneSearchStrategy> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_indexPath) || !IODirectory.Exists(_indexPath))
                return Task.FromResult(false);

            using var directory = FSDirectory.Open(_indexPath);
            using var reader = DirectoryReader.Open(directory);
            return Task.FromResult(reader.NumDocs >= 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task InitializeAsync(SearchConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _indexPath = configuration.IndexPath;
        
        if (string.IsNullOrEmpty(_indexPath))
        {
            throw new InvalidOperationException("Index path must be configured for Lucene search");
        }

        EnsureIndexDirectoryExists();
        _logger.LogInformation("Lucene search strategy initialized with index path: {IndexPath}", _indexPath);
        
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

        _logger.LogDebug("Lucene search for: {Query}", request.Query);

        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            using var reader = DirectoryReader.Open(directory);
            var searcher = new IndexSearcher(reader);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);

            // Build query based on search fields
            var searchFields = request.SearchFields?.ToArray() ?? new[] { "content", "title", "tags" };
            
            Query query;
            
            // For file searches, use WildcardQuery directly for substring matching
            var isFileSearch = request.DocumentTypes != null && request.DocumentTypes.Contains("file");
            
            if (isFileSearch && !request.MinSimilarity.HasValue)
            {
                // Build a BooleanQuery with WildcardQuery for each field to support substring matching
                var booleanQuery = new BooleanQuery();
                var wildcardPattern = $"*{request.Query.ToLowerInvariant()}*";
                
                foreach (var field in searchFields)
                {
                    var wildcardQuery = new WildcardQuery(new Term(field, wildcardPattern));
                    
                    // Apply field boost if specified
                    if (request.FieldBoosts != null && request.FieldBoosts.TryGetValue(field, out var boost))
                    {
                        wildcardQuery.Boost = boost;
                    }
                    
                    booleanQuery.Add(wildcardQuery, Occur.SHOULD);
                }
                
                query = booleanQuery;
            }
            else
            {
                // Use standard parser for non-file searches or fuzzy searches
                var parser = new MultiFieldQueryParser(LUCENE_VERSION, searchFields, analyzer, request.FieldBoosts);
                
                try
                {
                    var queryString = request.MinSimilarity.HasValue 
                        ? $"{request.Query}~" 
                        : request.Query;
                        
                    query = parser.Parse(queryString);
                }
                catch (ParseException)
                {
                    // Escape special characters if parsing fails
                    var escapedQuery = QueryParserBase.Escape(request.Query);
                    query = parser.Parse(escapedQuery);
                }
            }

            // Apply filters
            var filter = BuildFilter(request);
            
            _logger.LogDebug("Lucene query: {Query}, Filter: {HasFilter}", query.ToString(), filter != null);
            
            var topDocs = filter != null 
                ? searcher.Search(query, filter, 1000)
                : searcher.Search(query, 1000);

            var results = new List<SearchResult>();

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = searcher.Doc(scoreDoc.Doc);
                
                var result = new SearchResult
                {
                    DocumentId = doc.Get("id"),
                    Title = doc.Get("title"),
                    Slug = doc.Get("slug") ?? string.Empty,
                    Excerpt = doc.Get("contentsummary") ?? string.Empty,
                    Content = request.IncludeContent ? doc.Get("fullcontent") : null,
                    Score = scoreDoc.Score,
                    Author = doc.Get("modifiedby") ?? doc.Get("createdby"),
                    CreatedOn = DateTime.Parse(doc.Get("createdon")),
                    ModifiedOn = DateTime.Parse(doc.Get("modifiedon")),
                    Tags = doc.Get("tags")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>()
                };

                // Parse category if present
                var categoryIdStr = doc.Get("categoryid");
                if (!string.IsNullOrEmpty(categoryIdStr) && int.TryParse(categoryIdStr, out var categoryId))
                {
                    result.CategoryId = categoryId;
                    result.CategoryName = doc.Get("categoryname");
                }

                // Add file-specific metadata to highlights dictionary for easy access
                var documentType = doc.Get("documenttype");
                if (!string.IsNullOrEmpty(documentType))
                {
                    result.Highlights["DocumentType"] = new List<string> { documentType };
                    
                    var fileName = doc.Get("filename");
                    if (!string.IsNullOrEmpty(fileName))
                        result.Highlights["FileName"] = new List<string> { fileName };
                    
                    var fileExtension = doc.Get("fileextension");
                    if (!string.IsNullOrEmpty(fileExtension))
                        result.Highlights["FileExtension"] = new List<string> { fileExtension };
                    
                    var contentType = doc.Get("contenttype");
                    if (!string.IsNullOrEmpty(contentType))
                        result.Highlights["ContentType"] = new List<string> { contentType };
                    
                    var fileSize = doc.Get("filesize");
                    if (!string.IsNullOrEmpty(fileSize))
                        result.Highlights["FileSize"] = new List<string> { fileSize };
                    
                    var folderPath = doc.Get("folderpath");
                    if (!string.IsNullOrEmpty(folderPath))
                        result.Highlights["FolderPath"] = new List<string> { folderPath };
                    
                    var folderId = doc.Get("folderid");
                    if (!string.IsNullOrEmpty(folderId))
                        result.Highlights["FolderId"] = new List<string> { folderId };
                    
                    var visibility = doc.Get("visibility");
                    if (!string.IsNullOrEmpty(visibility))
                        result.Highlights["Visibility"] = new List<string> { visibility };
                }

                results.Add(result);
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Lucene index");
            
            return new SearchResponse
            {
                Query = request.Query,
                TotalResults = 0,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = 0,
                Results = new List<SearchResult>(),
                ExecutionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }
    }

    public async Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureIndexDirectoryExists();

            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };

            using var writer = new IndexWriter(directory, indexConfig);
            
            // Remove existing document for this ID
            writer.DeleteDocuments(new Term("id", document.Id));

            // Create new document
            var doc = CreateLuceneDocument(document);
            writer.AddDocument(doc);
            writer.Commit();

            _logger.LogDebug("Indexed document {DocumentId}: {Title}", document.Id, document.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document {DocumentId}", document.Id);
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task IndexDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureIndexDirectoryExists();

            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };

            using var writer = new IndexWriter(directory, indexConfig);
            
            foreach (var document in documents)
            {
                // Remove existing document
                writer.DeleteDocuments(new Term("id", document.Id));
                
                // Add new document
                var doc = CreateLuceneDocument(document);
                writer.AddDocument(doc);
            }
            
            writer.Commit();

            _logger.LogDebug("Indexed {Count} documents", documents.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing documents");
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task RemoveDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer);

            using var writer = new IndexWriter(directory, indexConfig);
            writer.DeleteDocuments(new Term("id", documentId));
            writer.Commit();

            _logger.LogDebug("Removed document {DocumentId} from index", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document {DocumentId} from index", documentId);
        }

        await Task.CompletedTask;
    }

    public async Task RemoveDocumentsAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer);

            using var writer = new IndexWriter(directory, indexConfig);
            
            foreach (var documentId in documentIds)
            {
                writer.DeleteDocuments(new Term("id", documentId));
            }
            
            writer.Commit();

            _logger.LogDebug("Removed {Count} documents from index", documentIds.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing documents from index");
        }

        await Task.CompletedTask;
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rebuilding Lucene search index");

        try
        {
            EnsureIndexDirectoryExists();

            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer)
            {
                OpenMode = OpenMode.CREATE // Recreate the index
            };

            using var writer = new IndexWriter(directory, indexConfig);
            writer.Commit();

            _logger.LogInformation("Lucene search index cleared and ready for rebuild");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding search index");
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task OptimizeIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer);

            using var writer = new IndexWriter(directory, indexConfig);
            writer.ForceMerge(1); // Optimize to single segment
            writer.Commit();

            _logger.LogInformation("Lucene search index optimized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing search index");
        }

        await Task.CompletedTask;
    }

    public async Task ClearIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer)
            {
                OpenMode = OpenMode.CREATE // Recreate empty index
            };

            using var writer = new IndexWriter(directory, indexConfig);
            writer.Commit();

            _logger.LogInformation("Lucene search index cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing search index");
        }

        await Task.CompletedTask;
    }

    public Task<SearchIndexStats> GetIndexStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IODirectory.Exists(_indexPath))
            {
                return Task.FromResult(new SearchIndexStats
                {
                    TotalDocuments = 0,
                    IndexSizeBytes = 0,
                    LastOptimized = null,
                    LastIndexed = null,
                    IsValid = false
                });
            }

            using var directory = FSDirectory.Open(_indexPath);
            using var reader = DirectoryReader.Open(directory);

            var indexSize = GetDirectorySize(new DirectoryInfo(_indexPath));

            return Task.FromResult(new SearchIndexStats
            {
                TotalDocuments = reader.NumDocs,
                IndexSizeBytes = indexSize,
                LastOptimized = null,
                LastIndexed = IODirectory.GetLastWriteTimeUtc(_indexPath),
                IsValid = true,
                AdditionalStats = new Dictionary<string, object>
                {
                    { "SearchEngine", "Lucene.NET" },
                    { "Version", Version },
                    { "IndexPath", _indexPath }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting index stats");
            return Task.FromResult(new SearchIndexStats
            {
                TotalDocuments = 0,
                IndexSizeBytes = 0,
                LastOptimized = null,
                LastIndexed = null,
                IsValid = false
            });
        }
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partialQuery) || partialQuery.Length < 2)
            return Enumerable.Empty<string>();

        try
        {
            var request = new SearchRequest
            {
                Query = $"{partialQuery}*",
                Page = 1,
                PageSize = maxSuggestions,
                SearchFields = new List<string> { "title" }
            };

            var results = await SearchAsync(request, cancellationToken);
            return results.Results.Select(r => r.Title).Distinct().Take(maxSuggestions);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<SearchResult>> FindSimilarAsync(string documentId, int count = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            using var reader = DirectoryReader.Open(directory);
            var searcher = new IndexSearcher(reader);

            // Find the document
            var query = new TermQuery(new Term("id", documentId));
            var topDocs = searcher.Search(query, 1);
            
            if (topDocs.TotalHits == 0)
                return Enumerable.Empty<SearchResult>();

            var doc = searcher.Doc(topDocs.ScoreDocs[0].Doc);
            var categoryId = doc.Get("categoryid");

            if (string.IsNullOrEmpty(categoryId))
                return Enumerable.Empty<SearchResult>();

            // Find similar documents in the same category
            var categoryQuery = new TermQuery(new Term("categoryid", categoryId));
            var similarDocs = searcher.Search(categoryQuery, count + 1);

            var results = new List<SearchResult>();
            foreach (var scoreDoc in similarDocs.ScoreDocs)
            {
                var similarDoc = searcher.Doc(scoreDoc.Doc);
                var id = similarDoc.Get("id");
                
                // Skip the original document
                if (id == documentId)
                    continue;

                results.Add(new SearchResult
                {
                    DocumentId = id,
                    Title = similarDoc.Get("title"),
                    Slug = similarDoc.Get("slug") ?? string.Empty,
                    Excerpt = similarDoc.Get("contentsummary") ?? string.Empty,
                    Score = scoreDoc.Score,
                    Author = similarDoc.Get("modifiedby") ?? similarDoc.Get("createdby"),
                    CreatedOn = DateTime.Parse(similarDoc.Get("createdon")),
                    ModifiedOn = DateTime.Parse(similarDoc.Get("modifiedon"))
                });

                if (results.Count >= count)
                    break;
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding similar documents");
            return Enumerable.Empty<SearchResult>();
        }
    }

    private Document CreateLuceneDocument(SearchDocument document)
    {
        var doc = new Document();
        
        // Stored and indexed fields
        doc.Add(new StringField("id", document.Id, Field.Store.YES));
        doc.Add(new TextField("title", document.Title ?? string.Empty, Field.Store.YES));
        doc.Add(new TextField("content", document.Content ?? string.Empty, Field.Store.NO));
        doc.Add(new StringField("slug", document.Slug ?? string.Empty, Field.Store.YES));
        
        // Tags
        var tags = string.Join(" ", document.Tags ?? Enumerable.Empty<string>());
        doc.Add(new TextField("tags", tags, Field.Store.YES));
        
        // Category
        if (document.CategoryId.HasValue)
        {
            doc.Add(new StringField("categoryid", document.CategoryId.Value.ToString(), Field.Store.YES));
            doc.Add(new StringField("categoryname", document.CategoryName ?? string.Empty, Field.Store.YES));
        }
        
        // Metadata
        doc.Add(new StringField("createdby", document.Author ?? string.Empty, Field.Store.YES));
        doc.Add(new StringField("modifiedby", document.Author ?? string.Empty, Field.Store.YES));
        doc.Add(new StringField("createdon", document.CreatedOn.ToString("O"), Field.Store.YES));
        doc.Add(new StringField("modifiedon", document.ModifiedOn.ToString("O"), Field.Store.YES));
        
        // File-specific metadata (if present) - use lowercase field names for consistency
        if (document.Metadata.TryGetValue("DocumentType", out var documentType))
        {
            doc.Add(new StringField("documenttype", documentType.ToLowerInvariant(), Field.Store.YES));
        }
        
        if (document.Metadata.TryGetValue("FileName", out var fileName))
        {
            // Index as both 'filename' (for searching) and 'title' (for display/boosting)
            doc.Add(new TextField("filename", fileName, Field.Store.YES));
            // Also add to title field if not already set for files
            if (string.IsNullOrEmpty(document.Title) || document.Title == fileName)
            {
                doc.Add(new TextField("title", fileName, Field.Store.YES));
            }
        }
        
        if (document.Metadata.TryGetValue("FileExtension", out var fileExtension))
        {
            doc.Add(new StringField("fileextension", fileExtension.ToLowerInvariant(), Field.Store.YES));
        }
        
        if (document.Metadata.TryGetValue("ContentType", out var contentType))
        {
            doc.Add(new StringField("contenttype", contentType, Field.Store.YES));
        }
        
        if (document.Metadata.TryGetValue("FileSize", out var fileSize))
        {
            doc.Add(new StringField("filesize", fileSize, Field.Store.YES));
        }
        
        if (document.Metadata.TryGetValue("FolderPath", out var folderPath))
        {
            doc.Add(new TextField("folderpath", folderPath, Field.Store.YES));
        }
        
        if (document.Metadata.TryGetValue("FolderId", out var folderId))
        {
            doc.Add(new StringField("folderid", folderId, Field.Store.YES));
        }
        
        if (document.Metadata.TryGetValue("Visibility", out var visibility))
        {
            doc.Add(new StringField("visibility", visibility, Field.Store.YES));
        }
        
        // Content summary (stored but not indexed)
        var summary = GenerateContentSummary(document.Content ?? string.Empty);
        doc.Add(new StoredField("contentsummary", summary));
        
        // Full content (stored but not indexed, for retrieval if requested)
        doc.Add(new StoredField("fullcontent", document.Content ?? string.Empty));

        return doc;
    }

    private Filter? BuildFilter(SearchRequest request)
    {
        var booleanQuery = new BooleanQuery();
        var hasFilters = false;

        // Category filter
        if (request.CategoryIds != null && request.CategoryIds.Any())
        {
            var categoryQuery = new BooleanQuery();
            foreach (var categoryId in request.CategoryIds)
            {
                categoryQuery.Add(new TermQuery(new Term("categoryid", categoryId.ToString())), Occur.SHOULD);
            }
            booleanQuery.Add(categoryQuery, Occur.MUST);
            hasFilters = true;
        }

        // Date range filter
        if (request.StartDate.HasValue || request.EndDate.HasValue)
        {
            var startDate = request.StartDate?.ToString("O") ?? DateTime.MinValue.ToString("O");
            var endDate = request.EndDate?.ToString("O") ?? DateTime.MaxValue.ToString("O");
            
            var dateQuery = TermRangeQuery.NewStringRange("modifiedon", startDate, endDate, true, true);
            booleanQuery.Add(dateQuery, Occur.MUST);
            hasFilters = true;
        }

        // Document type filter
        if (request.DocumentTypes != null && request.DocumentTypes.Any())
        {
            var docTypeQuery = new BooleanQuery();
            foreach (var docType in request.DocumentTypes)
            {
                docTypeQuery.Add(new TermQuery(new Term("documenttype", docType.ToLowerInvariant())), Occur.SHOULD);
            }
            booleanQuery.Add(docTypeQuery, Occur.MUST);
            hasFilters = true;
        }

        return hasFilters ? new QueryWrapperFilter(booleanQuery) : null;
    }

    private string GenerateContentSummary(string content, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // Remove HTML tags
        content = HtmlTagRegex.Replace(content, " ");
        content = Regex.Replace(content, @"\s+", " ").Trim();

        return content.Length > maxLength 
            ? content.Substring(0, maxLength) + "..." 
            : content;
    }

    private void EnsureIndexDirectoryExists()
    {
        if (!IODirectory.Exists(_indexPath))
        {
            IODirectory.CreateDirectory(_indexPath);
            _logger.LogInformation("Created Lucene index directory: {IndexPath}", _indexPath);
        }
    }

    private long GetDirectorySize(DirectoryInfo directory)
    {
        long size = 0;
        
        foreach (var file in directory.GetFiles())
            size += file.Length;
            
        foreach (var subDir in directory.GetDirectories())
            size += GetDirectorySize(subDir);
            
        return size;
    }
}
