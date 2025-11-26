using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Database.Repositories;
using Squirrel.Wiki.Core.Events;
using Squirrel.Wiki.Core.Events.Pages;
using Squirrel.Wiki.Core.Services.Caching;
using Squirrel.Wiki.Core.Services.Content;

namespace Squirrel.Wiki.Core.Services.Pages;

/// <summary>
/// Service implementation for managing links between pages
/// </summary>
public class PageLinkService : BaseService, IPageLinkService
{
    private readonly IPageRepository _pageRepository;
    private readonly IMarkdownService _markdownService;

    public PageLinkService(
        IPageRepository pageRepository,
        IMarkdownService markdownService,
        ILogger<PageLinkService> logger,
        ICacheService cacheService,
        IEventPublisher eventPublisher,
        IConfigurationService configuration)
        : base(logger, cacheService, eventPublisher, null, configuration)
    {
        _pageRepository = pageRepository;
        _markdownService = markdownService;
    }

    public async Task UpdateLinksToPageAsync(string oldTitle, string newTitle, CancellationToken cancellationToken = default)
    {
        LogInfo("Updating links from '{OldTitle}' to '{NewTitle}'", oldTitle, newTitle);

        var allPages = await _pageRepository.GetAllAsync(cancellationToken);

        foreach (var page in allPages)
        {
            var allVersions = await _pageRepository.GetAllContentVersionsAsync(page.Id, cancellationToken);
            var latestContent = allVersions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

            if (latestContent != null)
            {
                var updatedContent = await _markdownService.UpdatePageLinksAsync(
                    latestContent.Text, oldTitle, newTitle, cancellationToken);

                if (updatedContent != latestContent.Text)
                {
                    // Content was updated, create new version
                    var newVersion = new PageContent
                    {
                        PageId = page.Id,
                        Text = updatedContent,
                        VersionNumber = latestContent.VersionNumber + 1,
                        EditedOn = DateTime.UtcNow,
                        EditedBy = "System",
                        ChangeComment = $"Auto-updated link from '{oldTitle}' to '{newTitle}'"
                    };

                    await _pageRepository.AddContentVersionAsync(newVersion, cancellationToken);
                    
                    // Publish page updated event
                    var tags = page.PageTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>();
                    await EventPublisher.PublishAsync(
                        new PageUpdatedEvent(page.Id, page.Title, page.CategoryId, tags),
                        cancellationToken);
                }
            }
        }

        LogInfo("Completed updating links from '{OldTitle}' to '{NewTitle}'", oldTitle, newTitle);
    }
}
