namespace Squirrel.Wiki.Core.Events;

/// <summary>
/// Interface for handling domain events
/// Implement this interface to create handlers that respond to specific domain events
/// </summary>
/// <typeparam name="TEvent">The type of domain event this handler processes</typeparam>
public interface IEventHandler<in TEvent> where TEvent : DomainEvent
{
    /// <summary>
    /// Handles the specified domain event
    /// </summary>
    /// <param name="domainEvent">The domain event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
