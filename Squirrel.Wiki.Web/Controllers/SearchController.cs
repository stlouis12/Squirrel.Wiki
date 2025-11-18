using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Web.Models;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for search functionality
/// </summary>
public class SearchController : Controller
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        ISearchService searchService,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Displays search results
    /// </summary>
    /// <param name="q">Search query</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Results per page (default: 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery(Name = "query")] string q, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Search request received - Query: '{Query}', Page: {Page}, PageSize: {PageSize}", q, page, pageSize);
        
        if (string.IsNullOrWhiteSpace(q))
        {
            _logger.LogInformation("Empty search query, returning empty results");
            return View(new SearchResultsViewModel { Query = string.Empty });
        }

        try
        {
            // Check index stats first
            var indexStats = await _searchService.GetIndexStatsAsync(cancellationToken);
            _logger.LogInformation("Search index stats - Documents: {DocCount}, Valid: {IsValid}, Size: {Size} bytes", 
                indexStats.TotalDocuments, indexStats.IsValid, indexStats.IndexSizeBytes);
            
            if (!indexStats.IsValid || indexStats.TotalDocuments == 0)
            {
                _logger.LogWarning("Search index is empty or invalid. Documents: {DocCount}, Valid: {IsValid}", 
                    indexStats.TotalDocuments, indexStats.IsValid);
            }
            
            var results = await _searchService.SearchAsync(q, page, pageSize, cancellationToken);
            
            _logger.LogInformation("Search completed - Query: '{Query}', Results: {ResultCount}, Total: {TotalResults}", 
                q, results.Results.Count(), results.TotalResults);
            
            var viewModel = new SearchResultsViewModel
            {
                Query = q,
                Page = page,
                PageSize = pageSize,
                TotalResults = results.TotalResults,
                TotalPages = (int)Math.Ceiling((double)results.TotalResults / pageSize),
                Results = results.Results.Select(r => new SearchResultItemViewModel
                {
                    PageId = r.PageId,
                    Title = r.Title,
                    Slug = r.Slug ?? string.Empty,
                    Excerpt = r.Excerpt ?? string.Empty,
                    ModifiedBy = r.ModifiedBy ?? string.Empty,
                    ModifiedOn = r.ModifiedOn,
                    Score = r.Score
                }).ToList()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing search for query: {Query}", q);
            
            var errorViewModel = new SearchResultsViewModel
            {
                Query = q,
                Page = page,
                PageSize = pageSize
            };
            
            TempData["Error"] = "An error occurred while searching. Please try again.";
            return View(errorViewModel);
        }
    }

    /// <summary>
    /// Quick search API endpoint (for autocomplete/suggestions)
    /// </summary>
    /// <param name="q">Search query</param>
    /// <param name="limit">Maximum number of results (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    public async Task<IActionResult> Quick(string q, int limit = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(new List<object>());
        }

        try
        {
            var results = await _searchService.SearchAsync(q, 1, limit, cancellationToken);
            
            var suggestions = results.Results.Select(r => new
            {
                id = r.PageId,
                title = r.Title,
                slug = r.Slug,
                excerpt = TruncateExcerpt(r.Excerpt, 100)
            }).ToList();

            return Json(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing quick search for query: {Query}", q);
            return Json(new List<object>());
        }
    }

    /// <summary>
    /// Rebuilds the search index (admin only)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    // TODO: Add [Authorize(Policy = "RequireAdmin")] when authentication is implemented
    public async Task<IActionResult> RebuildIndex(CancellationToken cancellationToken)
    {
        try
        {
            await _searchService.RebuildIndexAsync(cancellationToken);
            
            _logger.LogInformation("Search index rebuilt successfully");
            TempData["Success"] = "Search index rebuilt successfully";
            
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding search index");
            TempData["Error"] = "An error occurred while rebuilding the search index";
            
            return RedirectToAction("Index", "Home");
        }
    }

    private static string TruncateExcerpt(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }
}
