using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Models;
using System.Text.RegularExpressions;
using LuceneDirectory = Lucene.Net.Store.Directory;
using IODirectory = System.IO.Directory;

namespace Squirrel.Wiki.Core.Services;

/// <summary>
/// Lucene.NET-based search service implementation for high-performance full-text search
/// </summary>
public class LuceneSearchService : BaseService, ISearchService
{
    private readonly IPageRepository _pageRepository;
    private readonly IMarkdownService _markdownService;
    private readonly string _indexPath;
    private static readonly LuceneVersion LUCENE_VERSION = LuceneVersion.LUCENE_48;
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);

    public LuceneSearchService(
        IPageRepository pageRepository,
        IMarkdownService markdownService,
        ILogger<LuceneSearchService> logger,
        ICacheService cache,
        ICacheInvalidationService cacheInvalidation,
        IOptions<SearchSettings> searchSettings)
        : base(logger, cache, cacheInvalidation, null)
    {
        _pageRepository = pageRepository;
        _markdownService = markdownService;
        _indexPath = searchSettings.Value.IndexPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "SearchIndex");
        
        EnsureIndexDirectoryExists();
    }

    public Task<SearchResultsDto> SearchAsync(string searchTerm, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Task.FromResult(new SearchResultsDto
            {
                Query = searchTerm,
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = 0
            });
        }

        LogInfo("Lucene search for: {SearchTerm}", searchTerm);

        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            using var reader = DirectoryReader.Open(directory);
            var searcher = new IndexSearcher(reader);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);

            // Search in both content and title fields
            var parser = new MultiFieldQueryParser(LUCENE_VERSION, new[] { "content", "title", "tags" }, analyzer);
            
            Query query;
            try
            {
                query = parser.Parse(searchTerm);
            }
            catch (ParseException)
            {
                // Escape special characters if parsing fails
                searchTerm = QueryParserBase.Escape(searchTerm);
                query = parser.Parse(searchTerm);
            }

            var topDocs = searcher.Search(query, 1000);
            var results = new List<SearchResultItemDto>();

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var doc = searcher.Doc(scoreDoc.Doc);
                results.Add(new SearchResultItemDto
                {
                    PageId = int.Parse(doc.Get("id")),
                    Title = doc.Get("title"),
                    Slug = doc.Get("slug") ?? "",
                    Excerpt = doc.Get("contentsummary") ?? "",
                    ModifiedBy = doc.Get("modifiedby") ?? doc.Get("createdby"),
                    ModifiedOn = DateTime.Parse(doc.Get("modifiedon")),
                    Score = scoreDoc.Score
                });
            }

            // Apply pagination
            var totalResults = results.Count;
            var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);
            var paginatedResults = results
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new SearchResultsDto
            {
                Query = searchTerm,
                Results = paginatedResults,
                TotalResults = totalResults,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            LogError(ex, "Error searching Lucene index");
            
            // Fall back to empty results
            return Task.FromResult(new SearchResultsDto
            {
                Query = searchTerm,
                Results = new List<SearchResultItemDto>(),
                TotalResults = 0,
                Page = pageNumber,
                PageSize = pageSize,
                TotalPages = 0
            });
        }
    }

    public async Task IndexPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
            if (page == null)
            {
                LogWarning("Page {PageId} not found for indexing", pageId);
                return;
            }

            var latestContent = await _pageRepository.GetLatestContentAsync(pageId, cancellationToken);
            if (latestContent == null)
            {
                LogWarning("No content found for page {PageId}", pageId);
                return;
            }

            EnsureIndexDirectoryExists();

            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND // Ensure we append to existing index or create new
            };

            using var writer = new IndexWriter(directory, indexConfig);
            
            // Remove existing document for this page
            writer.DeleteDocuments(new Term("id", pageId.ToString()));

            // Create new document
            var doc = new Document();
            
            // Stored and indexed fields
            doc.Add(new StringField("id", pageId.ToString(), Field.Store.YES));
            doc.Add(new TextField("title", page.Title ?? "", Field.Store.YES));
            doc.Add(new TextField("content", latestContent.Text ?? "", Field.Store.NO));
            doc.Add(new StringField("slug", page.Slug ?? "", Field.Store.YES));
            
            // Get tags
            var tags = string.Join(" ", page.PageTags?.Select(pt => pt.Tag.Name) ?? Enumerable.Empty<string>());
            doc.Add(new TextField("tags", tags, Field.Store.YES));
            
            // Metadata
            doc.Add(new StringField("createdby", page.CreatedBy ?? "", Field.Store.YES));
            doc.Add(new StringField("modifiedby", page.ModifiedBy ?? page.CreatedBy ?? "", Field.Store.YES));
            doc.Add(new StringField("modifiedon", page.ModifiedOn.ToString("O"), Field.Store.YES));
            
            // Content summary (stored but not indexed)
                var summary = await GenerateContentSummaryAsync(latestContent.Text ?? string.Empty, cancellationToken);
            doc.Add(new StoredField("contentsummary", summary));

            writer.AddDocument(doc);
            writer.Commit();

            LogDebug("Indexed page {PageId}: {Title}", pageId, page.Title);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error indexing page {PageId}", pageId);
            throw;
        }
    }

    public async Task IndexPagesAsync(IEnumerable<int> pageIds, CancellationToken cancellationToken = default)
    {
        foreach (var pageId in pageIds)
        {
            await IndexPageAsync(pageId, cancellationToken);
        }
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("Rebuilding search index");

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
            
            var allPages = await _pageRepository.GetAllAsync(cancellationToken);
            var pagesList = allPages.ToList();

            LogInfo("Indexing {Count} pages", pagesList.Count);

            foreach (var page in pagesList)
            {
                var latestContent = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
                if (latestContent == null)
                    continue;

                var doc = new Document();
                
                doc.Add(new StringField("id", page.Id.ToString(), Field.Store.YES));
                doc.Add(new TextField("title", page.Title ?? "", Field.Store.YES));
                doc.Add(new TextField("content", latestContent.Text ?? "", Field.Store.NO));
                doc.Add(new StringField("slug", page.Slug ?? "", Field.Store.YES));
                
                var tags = string.Join(" ", page.PageTags?.Select(pt => pt.Tag.Name) ?? Enumerable.Empty<string>());
                doc.Add(new TextField("tags", tags, Field.Store.YES));
                
                doc.Add(new StringField("createdby", page.CreatedBy ?? "", Field.Store.YES));
                doc.Add(new StringField("modifiedby", page.ModifiedBy ?? page.CreatedBy ?? "", Field.Store.YES));
                doc.Add(new StringField("modifiedon", page.ModifiedOn.ToString("O"), Field.Store.YES));
                
                var summary = await GenerateContentSummaryAsync(latestContent.Text ?? string.Empty, cancellationToken);
                doc.Add(new StoredField("contentsummary", summary));

                writer.AddDocument(doc);
            }

            writer.Commit();
            LogInfo("Search index rebuilt successfully with {Count} pages", pagesList.Count);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error rebuilding search index");
            throw;
        }
    }

    public async Task RemoveFromIndexAsync(int pageId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var directory = FSDirectory.Open(_indexPath);
            var analyzer = new StandardAnalyzer(LUCENE_VERSION);
            var indexConfig = new IndexWriterConfig(LUCENE_VERSION, analyzer);

            using var writer = new IndexWriter(directory, indexConfig);
            writer.DeleteDocuments(new Term("id", pageId.ToString()));
            writer.Commit();

            LogDebug("Removed page {PageId} from index", pageId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error removing page {PageId} from index", pageId);
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

            LogInfo("Search index optimized");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error optimizing search index");
        }

        await Task.CompletedTask;
    }

    public Task<SearchIndexStatsDto> GetIndexStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IODirectory.Exists(_indexPath))
            {
                return Task.FromResult(new SearchIndexStatsDto
                {
                    TotalDocuments = 0,
                    IndexSizeBytes = 0,
                    LastOptimized = DateTime.MinValue,
                    IsValid = false
                });
            }

            using var directory = FSDirectory.Open(_indexPath);
            using var reader = DirectoryReader.Open(directory);

            var indexSize = GetDirectorySize(new DirectoryInfo(_indexPath));

            return Task.FromResult(new SearchIndexStatsDto
            {
                TotalDocuments = reader.NumDocs,
                IndexSizeBytes = indexSize,
                LastOptimized = DateTime.UtcNow, // Could be tracked separately
                IsValid = true
            });
        }
        catch (Exception ex)
        {
            LogError(ex, "Error getting index stats");
            return Task.FromResult(new SearchIndexStatsDto
            {
                TotalDocuments = 0,
                IndexSizeBytes = 0,
                LastOptimized = DateTime.MinValue,
                IsValid = false
            });
        }
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

            LogInfo("Search index cleared");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error clearing search index");
        }

        await Task.CompletedTask;
    }

    public Task<bool> IsIndexValidAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IODirectory.Exists(_indexPath))
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

    // Delegate other search methods to database-based implementation for now
    // These can be enhanced with Lucene later if needed

    public Task<SearchResultsDto> SearchByTagAsync(string tag, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // Could be implemented with Lucene tag field search
        return SearchAsync($"tags:{tag}", pageNumber, pageSize, cancellationToken);
    }

    public async Task<SearchResultsDto> SearchByCategoryAsync(int categoryId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // Fall back to database for category search
        var pages = await _pageRepository.GetByCategoryAsync(categoryId, cancellationToken);
        var results = new List<SearchResultItemDto>();

        foreach (var page in pages)
        {
            var latestContent = await _pageRepository.GetLatestContentAsync(page.Id, cancellationToken);
            if (latestContent != null)
            {
                results.Add(new SearchResultItemDto
                {
                    PageId = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    Excerpt = await GenerateContentSummaryAsync(latestContent.Text, cancellationToken),
                    ModifiedBy = page.ModifiedBy ?? page.CreatedBy,
                    ModifiedOn = page.ModifiedOn,
                    Score = 1.0f
                });
            }
        }

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
        // Could be implemented with Lucene createdby/modifiedby field search
        return await SearchAsync($"createdby:{author} OR modifiedby:{author}", pageNumber, pageSize, cancellationToken);
    }

    public Task<SearchResultsDto> AdvancedSearchAsync(SearchQueryDto searchQuery, CancellationToken cancellationToken = default)
    {
        // Build Lucene query from SearchQueryDto
        var queryParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(searchQuery.Query))
            queryParts.Add(searchQuery.Query);
            
        if (!string.IsNullOrWhiteSpace(searchQuery.Author))
            queryParts.Add($"(createdby:{searchQuery.Author} OR modifiedby:{searchQuery.Author})");
            
        if (searchQuery.Tags != null && searchQuery.Tags.Any())
            queryParts.Add($"tags:({string.Join(" OR ", searchQuery.Tags)})");

        var combinedQuery = string.Join(" AND ", queryParts);
        return SearchAsync(combinedQuery, searchQuery.Page, searchQuery.PageSize, cancellationToken);
    }

    public Task<SearchResultsDto> SearchInCategoryAsync(string query, int categoryId, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // Combine with category filter
        return SearchByCategoryAsync(categoryId, pageNum, pageSize, cancellationToken);
    }

    public Task<SearchResultsDto> SearchByTagsAsync(List<string> tags, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var tagQuery = string.Join(" OR ", tags.Select(t => $"tags:{t}"));
        return SearchAsync(tagQuery, pageNum, pageSize, cancellationToken);
    }

    public Task<SearchResultsDto> SearchByDateRangeAsync(DateTime? startDate, DateTime? endDate, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // Date range search would require special handling in Lucene
        // For now, fall back to basic search
        return SearchAsync("*:*", pageNumber, pageSize, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        return await GetSearchSuggestionsAsync(partialQuery, maxSuggestions, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetSearchSuggestionsAsync(string partialTerm, int maxSuggestions = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partialTerm) || partialTerm.Length < 2)
            return Enumerable.Empty<string>();

        try
        {
            var results = await SearchAsync($"{partialTerm}*", 1, maxSuggestions, cancellationToken);
            return results.Results.Select(r => r.Title).Distinct().Take(maxSuggestions);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<PageDto>> GetSimilarPagesAsync(int pageId, int count = 5, CancellationToken cancellationToken = default)
    {
        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null || !page.CategoryId.HasValue)
            return Enumerable.Empty<PageDto>();

        var similarPages = await _pageRepository.GetByCategoryAsync(page.CategoryId.Value, cancellationToken);
        
        return similarPages
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
    }

    public Task<SearchResultsDto> FuzzySearchAsync(string query, float minSimilarity = 0.7f, int pageNum = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // Lucene supports fuzzy search with ~ operator
        var fuzzyQuery = $"{query}~";
        return SearchAsync(fuzzyQuery, pageNum, pageSize, cancellationToken);
    }

    private async Task<string> GenerateContentSummaryAsync(string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // Convert markdown to HTML then strip tags
        var html = await _markdownService.ToHtmlAsync(content, cancellationToken);
        html = HtmlTagRegex.Replace(html, " ");
        html = Regex.Replace(html, @"\s+", " ").Trim();

        return html.Length > 150 ? html.Substring(0, 150) + "..." : html;
    }

    private void EnsureIndexDirectoryExists()
    {
        if (!IODirectory.Exists(_indexPath))
        {
            IODirectory.CreateDirectory(_indexPath);
            LogInfo("Created search index directory: {IndexPath}", _indexPath);
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

/// <summary>
/// Configuration settings for Lucene search
/// </summary>
public class SearchSettings
{
    public string IndexPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "SearchIndex");
}

