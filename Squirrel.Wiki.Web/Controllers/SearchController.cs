using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Services;
using Squirrel.Wiki.Core.Security;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Web.Models;
using Squirrel.Wiki.Web.Services;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for search functionality
/// </summary>
public class SearchController : BaseController
{
    private readonly ISearchService _searchService;
    private readonly Squirrel.Wiki.Core.Security.IAuthorizationService _authorizationService;
    private readonly IPageRepository _pageRepository;

    public SearchController(
        ISearchService searchService,
        Squirrel.Wiki.Core.Security.IAuthorizationService authorizationService,
        IPageRepository pageRepository,
        ILogger<SearchController> logger,
        INotificationService notifications)
        : base(logger, notifications)
    {
        _searchService = searchService;
        _authorizationService = authorizationService;
        _pageRepository = pageRepository;
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
            
            // Filter results based on authorization
            var authorizedResults = new List<SearchResultItemViewModel>();
            foreach (var result in results.Results)
            {
                var pageEntity = await _pageRepository.GetByIdAsync(result.PageId, cancellationToken);
                if (pageEntity != null && await _authorizationService.CanViewPageAsync(pageEntity, cancellationToken))
                {
                    authorizedResults.Add(new SearchResultItemViewModel
                    {
                        PageId = result.PageId,
                        Title = result.Title,
                        Slug = result.Slug ?? string.Empty,
                        Excerpt = result.Excerpt ?? string.Empty,
                        ModifiedBy = result.ModifiedBy ?? string.Empty,
                        ModifiedOn = result.ModifiedOn,
                        Score = result.Score
                    });
                }
            }
            
            var viewModel = new SearchResultsViewModel
            {
                Query = q,
                Page = page,
                PageSize = pageSize,
                TotalResults = authorizedResults.Count, // Use filtered count
                TotalPages = (int)Math.Ceiling((double)authorizedResults.Count / pageSize),
                Results = authorizedResults
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
            
            NotifyError("An error occurred while searching. Please try again.");
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
            
            // Filter results based on authorization
            var authorizedSuggestions = new List<object>();
            foreach (var result in results.Results)
            {
                var pageEntity = await _pageRepository.GetByIdAsync(result.PageId, cancellationToken);
                if (pageEntity != null && await _authorizationService.CanViewPageAsync(pageEntity, cancellationToken))
                {
                    authorizedSuggestions.Add(new
                    {
                        id = result.PageId,
                        title = result.Title,
                        slug = result.Slug,
                        excerpt = TruncateExcerpt(result.Excerpt, 100)
                    });
                }
            }

            return Json(authorizedSuggestions);
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
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> RebuildIndex(CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            await _searchService.RebuildIndexAsync(cancellationToken);
            
            _logger.LogInformation("Search index rebuilt successfully");
            NotifySuccess("Search index rebuilt successfully");
            
            return RedirectToAction("Index", "Home");
        },
        ex =>
        {
            NotifyError("An error occurred while rebuilding the search index");
            return RedirectToAction("Index", "Home");
        });
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
