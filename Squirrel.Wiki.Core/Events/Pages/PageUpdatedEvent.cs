namespace Squirrel.Wiki.Core.Events.Pages;

/// <summary>
/// Event raised when a page is updated
/// </summary>
public class PageUpdatedEvent : DomainEvent
{
    public int PageId { get; }
    public string Title { get; }
    public int? CategoryId { get; }
    public List<string> Tags { get; }

    public PageUpdatedEvent(int pageId, string title, int? categoryId, List<string> tags)
    {
        PageId = pageId;
        Title = title;
        CategoryId = categoryId;
        Tags = tags ?? new List<string>();
    }
}
