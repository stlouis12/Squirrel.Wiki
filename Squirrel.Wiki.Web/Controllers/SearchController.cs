using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Web.Models;
using Squirrel.Wiki.Web.Services;
using Squirrel.Wiki.Core.Services.Search;
using Squirrel.Wiki.Core.Services.Configuration;

namespace Squirrel.Wiki.Web.Controllers;

/// <summary>
/// Controller for search functionality
/// </summary>
public class SearchController : BaseController
{
    private readonly ISearchService _searchService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPageRepository _pageRepository;

    public SearchController(
        ISearchService searchService,
        IAuthorizationService authorizationService,
        IPageRepository pageRepository,
        ITimezoneService timezoneService,
        ILogger<SearchController> logger,
        INotificationService notifications)
        : base(logger, notifications, timezoneService, null)
    {
        _searchService = searchService;
        _authorizationService = authorizationService;
        _pageRepository = pageRepository;
    }

    /// <summary>
    /// Displays search results - Refactored with Result Pattern
    /// </summary>
    /// <param name="q">Search query</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Results per page (default: 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery(Name = "query")] string q, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Search request received - Query: '{Query}', Page: {Page}, PageSize: {PageSize}", q, page, pageSize);
        
        // Handle empty query early
        if (string.IsNullOrWhiteSpace(q))
        {
            _logger.LogInformation("Empty search query, returning empty results");
            return View(new SearchResultsViewModel { Query = string.Empty });
        }

        // Execute search with Result Pattern for clean error handling
        var result = await ExecuteSearchWithResult(q, page, pageSize, cancellationToken);
        
        // Use Result Pattern to handle success/failure
        return result.Match<IActionResult>(
            onSuccess: viewModel =>
            {
                PopulateBaseViewModel(viewModel);
                return View(viewModel);
            },
            onFailure: (error, code) =>
            {
                _logger.LogError("Search failed: {Error}", error);
                
                var errorViewModel = new SearchResultsViewModel
                {
                    Query = q,
                    Page = page,
                    PageSize = pageSize
                };
                
                PopulateBaseViewModel(errorViewModel);
                NotifyError("An error occurred while searching. Please try again.");
                return View(errorViewModel);
            }
        );
    }

    /// <summary>
    /// Quick search API endpoint (for autocomplete/suggestions) - Refactored with Result Pattern
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

        // Execute quick search with Result Pattern
        var result = await ExecuteQuickSearchWithResult(q, limit, cancellationToken);
        
        // Use Match for custom handling - returns empty array on failure
        return result.Match<IActionResult>(
            onSuccess: suggestions => Json(suggestions),
            onFailure: (error, code) =>
            {
                _logger.LogWarning("Quick search failed: {Error}", error);
                return Json(new List<object>());
            }
        );
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
            NotifyLocalizedSuccess("Notification_SearchIndexRebuilt");
            
            return RedirectToAction("Index", "Home");
        });
    }

    #region Helper Methods

    /// <summary>
    /// Executes search operation with Result Pattern
    /// Encapsulates all search logic including authorization filtering
    /// </summary>
    private async Task<Result<SearchResultsViewModel>> ExecuteSearchWithResult(
        string query, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken)
    {
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
            
            // Perform search
            var results = await _searchService.SearchAsync(query, page, pageSize, cancellationToken);
            
            _logger.LogInformation("Search completed - Query: '{Query}', Results: {ResultCount}, Total: {TotalResults}", 
                query, results.Results.Count, results.TotalResults);
            
            // Filter results based on authorization using new policy-based authorization
            var authorizedResults = new List<SearchResultItemViewModel>();
            foreach (var result in results.Results)
            {
                var pageEntity = await _pageRepository.GetByIdAsync(result.PageId, cancellationToken);
                if (pageEntity != null && await CanViewPageAsync(_authorizationService, pageEntity))
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
            
            // Build view model
            var viewModel = new SearchResultsViewModel
            {
                Query = query,
                Page = page,
                PageSize = pageSize,
                TotalResults = authorizedResults.Count,
                TotalPages = (int)Math.Ceiling((double)authorizedResults.Count / pageSize),
                Results = authorizedResults
            };

            return Result<SearchResultsViewModel>.Success(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing search for query: {Query}", query);
            return Result<SearchResultsViewModel>.Failure(
                "An error occurred while searching. Please try again.", 
                "SEARCH_ERROR");
        }
    }

    /// <summary>
    /// Executes quick search operation with Result Pattern
    /// Used for autocomplete/suggestions
    /// </summary>
    private async Task<Result<List<object>>> ExecuteQuickSearchWithResult(
        string query, 
        int limit, 
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _searchService.SearchAsync(query, 1, limit, cancellationToken);
            
            // Filter results based on authorization using new policy-based authorization
            var authorizedSuggestions = new List<object>();
            foreach (var result in results.Results)
            {
                var pageEntity = await _pageRepository.GetByIdAsync(result.PageId, cancellationToken);
                if (pageEntity != null && await CanViewPageAsync(_authorizationService, pageEntity))
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

            return Result<List<object>>.Success(authorizedSuggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing quick search for query: {Query}", query);
            return Result<List<object>>.Failure("Quick search failed", "QUICK_SEARCH_ERROR");
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

    #endregion
}
