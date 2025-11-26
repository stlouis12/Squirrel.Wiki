namespace Squirrel.Wiki.Core.Events.Categories;

/// <summary>
/// Event raised when a category is created, updated, or deleted
/// Used for cache invalidation purposes
/// </summary>
public class CategoryChangedEvent : DomainEvent
{
    public int CategoryId { get; }
    public string CategoryName { get; }

    public CategoryChangedEvent(int categoryId, string categoryName)
    {
        CategoryId = categoryId;
        CategoryName = categoryName;
    }
}
