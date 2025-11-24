namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found
/// </summary>
public class EntityNotFoundException : SquirrelWikiException
{
    public string EntityName { get; }
    public object EntityId { get; }

    public EntityNotFoundException(string entityName, object id)
        : base($"{entityName} with ID {id} not found")
    {
        EntityName = entityName;
        EntityId = id;
    }

    public EntityNotFoundException(string entityName, object id, Exception innerException)
        : base($"{entityName} with ID {id} not found", innerException)
    {
        EntityName = entityName;
        EntityId = id;
    }
}
