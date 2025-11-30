using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Events.Pages;
using Squirrel.Wiki.Core.Events.Search;

namespace Squirrel.Wiki.Core.Events.Handlers;

/// <summary>
/// Handles page events and triggers search index updates
/// This bridges page lifecycle events to search indexing events
/// </summary>
public class PageSearchIndexHandler :
    IEventHandler<PageCreatedEvent>,
    IEventHandler<PageUpdatedEvent>,
    IEventHandler<PageDeletedEvent>
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<PageSearchIndexHandler> _logger;

    public PageSearchIndexHandler(
        IEventPublisher eventPublisher,
        ILogger<PageSearchIndexHandler> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task HandleAsync(PageCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Page created, triggering search index update for page {PageId} - {Title}",
            domainEvent.PageId, domainEvent.Title);

        // Trigger incremental index update for this page
        await _eventPublisher.PublishAsync(
            new PageIndexRequestedEvent(domainEvent.PageId, domainEvent.Title, string.Empty),
            cancellationToken);
    }

    public async Task HandleAsync(PageUpdatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Page updated, triggering search index update for page {PageId} - {Title}",
            domainEvent.PageId, domainEvent.Title);

        // Trigger incremental index update for this page
        await _eventPublisher.PublishAsync(
            new PageIndexRequestedEvent(domainEvent.PageId, domainEvent.Title, string.Empty),
            cancellationToken);
    }

    public async Task HandleAsync(PageDeletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Page deleted, triggering search index removal for page {PageId} - {Title}",
            domainEvent.PageId, domainEvent.Title);

        // Trigger removal from search index
        await _eventPublisher.PublishAsync(
            new PageRemovedFromIndexEvent(domainEvent.PageId),
            cancellationToken);
    }
}
