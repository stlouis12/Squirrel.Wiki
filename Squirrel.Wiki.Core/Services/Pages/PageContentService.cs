using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Events.Pages;
using Squirrel.Wiki.Core.Exceptions;
using Squirrel.Wiki.Core.Models;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Configuration;

namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service implementation for page content versioning and history
/// </summary>
public class PageContentService : BaseService, IPageContentService
{
    private readonly IPageRepository _pageRepository;

    public PageContentService(
        IPageRepository pageRepository,
        ILogger<PageContentService> logger,
        ICacheService cacheService,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cacheService, eventPublisher, null, configuration)
    {
        _pageRepository = pageRepository;
    }

    public async Task<PageContentDto> GetLatestContentAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var content = await _pageRepository.GetLatestContentAsync(pageId, cancellationToken);
        if (content == null)
            throw new EntityNotFoundException("PageContent", pageId);

        return MapToPageContentDto(content);
    }

    public async Task<IEnumerable<PageContentDto>> GetContentHistoryAsync(int pageId, CancellationToken cancellationToken = default)
    {
        var versions = await _pageRepository.GetAllContentVersionsAsync(pageId, cancellationToken);
        return versions.Select(MapToPageContentDto).OrderByDescending(v => v.Version);
    }

    public async Task<PageContentDto?> GetContentByVersionAsync(int pageId, int version, CancellationToken cancellationToken = default)
    {
        var content = await _pageRepository.GetContentByVersionAsync(pageId, version, cancellationToken);
        return content != null ? MapToPageContentDto(content) : null;
    }

    public async Task<PageDto> RevertToVersionAsync(int pageId, int version, string username, CancellationToken cancellationToken = default)
    {
        LogInfo("Reverting page {PageId} to version {Version}", pageId, version);

        var oldContent = await _pageRepository.GetContentByVersionAsync(pageId, version, cancellationToken);
        if (oldContent == null)
            throw new EntityNotFoundException("PageContent", $"Page {pageId}, Version {version}");

        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page == null)
            throw new EntityNotFoundException("Page", pageId);

        // Get current max version
        var allVersions = await _pageRepository.GetAllContentVersionsAsync(pageId, cancellationToken);
        var maxVersion = allVersions.Max(v => v.VersionNumber);

        // Create new version with old content
        var newContent = new PageContent
        {
            PageId = pageId,
            Text = oldContent.Text,
            VersionNumber = maxVersion + 1,
            EditedOn = DateTime.UtcNow,
            EditedBy = username,
            ChangeComment = $"Reverted to version {version}"
        };

        await _pageRepository.AddContentVersionAsync(newContent, cancellationToken);

        page.ModifiedOn = DateTime.UtcNow;
        page.ModifiedBy = username;
        await _pageRepository.UpdateAsync(page, cancellationToken);

        // Publish page updated event
        var tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>();
        await EventPublisher.PublishAsync(
            new PageUpdatedEvent(page.Id, page.Title, page.CategoryId, tags),
            cancellationToken);

        LogInfo("Page reverted successfully: {PageId}", pageId);

        // Return the page DTO (we'll need to construct it)
        return new PageDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            CategoryId = page.CategoryId,
            Visibility = page.Visibility,
            IsLocked = page.IsLocked,
            IsDeleted = page.IsDeleted,
            CreatedOn = page.CreatedOn,
            CreatedBy = page.CreatedBy,
            ModifiedOn = page.ModifiedOn,
            ModifiedBy = page.ModifiedBy,
            Content = newContent.Text,
            Version = newContent.VersionNumber,
            Tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>()
        };
    }

    public async Task CreateContentVersionAsync(int pageId, string content, string username, string? changeComment = null, CancellationToken cancellationToken = default)
    {
        LogDebug("Creating new content version for page {PageId}", pageId);

        var allVersions = await _pageRepository.GetAllContentVersionsAsync(pageId, cancellationToken);
        var maxVersion = allVersions.Any() ? allVersions.Max(v => v.VersionNumber) : 0;

        var pageContent = new PageContent
        {
            PageId = pageId,
            Text = content,
            VersionNumber = maxVersion + 1,
            EditedOn = DateTime.UtcNow,
            EditedBy = username,
            ChangeComment = changeComment
        };

        await _pageRepository.AddContentVersionAsync(pageContent, cancellationToken);

        // Publish page updated event
        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page != null)
        {
            var tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>();
            await EventPublisher.PublishAsync(
                new PageUpdatedEvent(page.Id, page.Title, page.CategoryId, tags),
                cancellationToken);
        }

        LogDebug("Content version created for page {PageId}, version {Version}", pageId, pageContent.VersionNumber);
    }

    public async Task UpdateContentVersionAsync(int pageId, string content, string username, string? changeComment = null, CancellationToken cancellationToken = default)
    {
        LogDebug("Updating existing content version for page {PageId}", pageId);

        var existingContent = await _pageRepository.GetLatestContentAsync(pageId, cancellationToken);

        if (existingContent != null)
        {
            // Update the existing content
            existingContent.Text = content;
            existingContent.EditedOn = DateTime.UtcNow;
            existingContent.EditedBy = username;
            existingContent.ChangeComment = changeComment;

            await _pageRepository.UpdateContentVersionAsync(existingContent, cancellationToken);
        }
        else
        {
            // No existing content found, create initial version
            var pageContent = new PageContent
            {
                PageId = pageId,
                Text = content,
                VersionNumber = 1,
                EditedOn = DateTime.UtcNow,
                EditedBy = username,
                ChangeComment = changeComment
            };

            await _pageRepository.AddContentVersionAsync(pageContent, cancellationToken);
        }

        // Publish page updated event
        var page = await _pageRepository.GetByIdAsync(pageId, cancellationToken);
        if (page != null)
        {
            var tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>();
            await EventPublisher.PublishAsync(
                new PageUpdatedEvent(page.Id, page.Title, page.CategoryId, tags),
                cancellationToken);
        }

        LogDebug("Content version updated for page {PageId}", pageId);
    }

    private PageContentDto MapToPageContentDto(PageContent content)
    {
        return new PageContentDto
        {
            Id = content.Id,
            PageId = content.PageId,
            Content = content.Text,
            Version = content.VersionNumber,
            CreatedOn = content.EditedOn,
            CreatedBy = content.EditedBy,
            ChangeComment = content.ChangeComment
        };
    }
}
