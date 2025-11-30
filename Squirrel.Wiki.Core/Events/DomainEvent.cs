namespace Squirrel.Wiki.Core.Events;

/// <summary>
/// Base class for all domain events in the system
/// Domain events represent significant business occurrences that other parts of the system may need to react to
/// </summary>
public abstract class DomainEvent
{
    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// Unique identifier for this event instance
    /// </summary>
    public string EventId { get; } = Guid.NewGuid().ToString();
}
