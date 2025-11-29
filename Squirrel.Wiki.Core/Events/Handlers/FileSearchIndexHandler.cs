using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Events.Files;
using Squirrel.Wiki.Core.Services.Search;

namespace Squirrel.Wiki.Core.Events.Handlers;

/// <summary>
/// Handles file events and triggers search index updates
/// This bridges file lifecycle events to search indexing operations
/// </summary>
public class FileSearchIndexHandler :
    IEventHandler<FileUploadedEvent>,
    IEventHandler<FileUpdatedEvent>,
    IEventHandler<FileDeletedEvent>,
    IEventHandler<FileMovedEvent>
{
    private readonly ISearchService _searchService;
    private readonly ILogger<FileSearchIndexHandler> _logger;

    public FileSearchIndexHandler(
        ISearchService searchService,
        ILogger<FileSearchIndexHandler> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public async Task HandleAsync(FileUploadedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("File uploaded, triggering search index update for file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);

            await _searchService.IndexFileAsync(domainEvent.FileId, cancellationToken);
            
            _logger.LogInformation("Successfully indexed file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing uploaded file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);
            // Don't throw - indexing failures shouldn't break file upload
        }
    }

    public async Task HandleAsync(FileUpdatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("File updated, triggering search index update for file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);

            await _searchService.IndexFileAsync(domainEvent.FileId, cancellationToken);
            
            _logger.LogInformation("Successfully re-indexed file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-indexing updated file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);
            // Don't throw - indexing failures shouldn't break file update
        }
    }

    public async Task HandleAsync(FileDeletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("File deleted, triggering search index removal for file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);

            await _searchService.RemoveFileFromIndexAsync(domainEvent.FileId, cancellationToken);
            
            _logger.LogInformation("Successfully removed file {FileId} - {FileName} from search index",
                domainEvent.FileId, domainEvent.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing deleted file {FileId} - {FileName} from search index",
                domainEvent.FileId, domainEvent.FileName);
            // Don't throw - indexing failures shouldn't break file deletion
        }
    }

    public async Task HandleAsync(FileMovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("File moved, triggering search index update for file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);

            // Re-index the file to update folder path information
            await _searchService.IndexFileAsync(domainEvent.FileId, cancellationToken);
            
            _logger.LogInformation("Successfully re-indexed moved file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-indexing moved file {FileId} - {FileName}",
                domainEvent.FileId, domainEvent.FileName);
            // Don't throw - indexing failures shouldn't break file move
        }
    }
}
