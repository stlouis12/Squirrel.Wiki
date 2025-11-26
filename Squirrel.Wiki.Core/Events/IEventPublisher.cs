namespace Squirrel.Wiki.Core.Events;

/// <summary>
/// Interface for publishing domain events to registered handlers
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event to all registered handlers
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event to publish</typeparam>
    /// <param name="domainEvent">The domain event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent;
}
