namespace Squirrel.Wiki.Core.Events.Pages;

/// <summary>
/// Event raised when a page is deleted
/// </summary>
public class PageDeletedEvent : DomainEvent
{
    public int PageId { get; }
    public string Title { get; }

    public PageDeletedEvent(int pageId, string title)
    {
        PageId = pageId;
        Title = title;
    }
}
