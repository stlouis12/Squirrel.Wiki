namespace Squirrel.Wiki.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found
/// </summary>
public class EntityNotFoundException : SquirrelWikiException
{
    public string EntityType { get; }
    public object EntityId { get; }
    
    public EntityNotFoundException(string entityType, object entityId)
        : base(
            $"{entityType} with ID '{entityId}' was not found",
            "ENTITY_NOT_FOUND",
            statusCode: 404,
            shouldLog: false) // Not found is expected, don't log as error
    {
        EntityType = entityType;
        EntityId = entityId;
        
        Context["EntityType"] = entityType;
        Context["EntityId"] = entityId;
    }
    
    public EntityNotFoundException(string entityType, object entityId, string additionalInfo)
        : base(
            $"{entityType} with ID '{entityId}' was not found. {additionalInfo}",
            "ENTITY_NOT_FOUND",
            statusCode: 404,
            shouldLog: false)
    {
        EntityType = entityType;
        EntityId = entityId;
        
        Context["EntityType"] = entityType;
        Context["EntityId"] = entityId;
        Context["AdditionalInfo"] = additionalInfo;
    }
    
    public override string GetUserMessage()
    {
        return $"The requested {EntityType.ToLower()} could not be found.";
    }
}
