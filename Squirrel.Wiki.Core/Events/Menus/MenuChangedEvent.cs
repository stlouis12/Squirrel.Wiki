namespace Squirrel.Wiki.Core.Events.Menus;

/// <summary>
/// Event raised when a menu is created, updated, or deleted
/// Used for cache invalidation purposes
/// </summary>
public class MenuChangedEvent : DomainEvent
{
    public int MenuId { get; }
    public string MenuName { get; }

    public MenuChangedEvent(int menuId, string menuName)
    {
        MenuId = menuId;
        MenuName = menuName;
    }
}
