namespace Squirrel.Wiki.Core.Events.Pages;

/// <summary>
/// Event raised when a new page is created
/// </summary>
public class PageCreatedEvent : DomainEvent
{
    public int PageId { get; }
    public string Title { get; }
    public int? CategoryId { get; }
    public List<string> Tags { get; }

    public PageCreatedEvent(int pageId, string title, int? categoryId, List<string> tags)
    {
        PageId = pageId;
        Title = title;
        CategoryId = categoryId;
        Tags = tags ?? new List<string>();
    }
}
