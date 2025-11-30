namespace Squirrel.Wiki.Core.Events.Tags;

/// <summary>
/// Event raised when a tag is created, updated, or deleted
/// Used for cache invalidation purposes
/// </summary>
public class TagChangedEvent : DomainEvent
{
    public string TagName { get; }

    public TagChangedEvent(string tagName)
    {
        TagName = tagName;
    }
}
