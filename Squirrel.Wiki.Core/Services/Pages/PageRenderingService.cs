using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Content;

namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service implementation for rendering page content from Markdown to HTML
/// </summary>
public class PageRenderingService : BaseService, IPageRenderingService
{
    private readonly IPageRepository _pageRepository;
    private readonly IMarkdownService _markdownService;

    public PageRenderingService(
        IPageRepository pageRepository,
        IMarkdownService markdownService,
        ILogger<PageRenderingService> logger,
        ICacheService cacheService,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cacheService, eventPublisher, null, configuration)
    {
        _pageRepository = pageRepository;
        _markdownService = markdownService;
    }

    public async Task<string> RenderContentAsync(string markdownContent, CancellationToken cancellationToken = default)
    {
        var html = await _markdownService.ToHtmlAsync(markdownContent, cancellationToken);
        return await _markdownService.ProcessTokensAsync(html, cancellationToken);
    }

    public async Task<string> RenderPageAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var content = await _pageRepository.GetLatestContentAsync(pageId, cancellationToken);
        if (content == null)
            throw new EntityNotFoundException("PageContent", pageId);

        return await RenderContentAsync(content.Text, cancellationToken);
    }
}
