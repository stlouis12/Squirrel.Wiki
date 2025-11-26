using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Squirrel.Wiki.Core.Events;

/// <summary>
/// Default implementation of IEventPublisher that uses dependency injection to locate and invoke event handlers
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IServiceProvider serviceProvider, ILogger<EventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent
    {
        var eventType = typeof(TEvent);
        _logger.LogDebug("Publishing event: {EventType} (ID: {EventId})", 
            eventType.Name, domainEvent.EventId);

        // Get all handlers for this event type
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType);

        var handlersList = handlers.ToList();
        if (!handlersList.Any())
        {
            _logger.LogDebug("No handlers registered for event type: {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Found {HandlerCount} handler(s) for event type: {EventType}", 
            handlersList.Count, eventType.Name);

        // Execute all handlers
        foreach (var handler in handlersList)
        {
            try
            {
                var handleMethod = handlerType.GetMethod(nameof(IEventHandler<TEvent>.HandleAsync));
                if (handleMethod != null)
                {
                    var task = (Task)handleMethod.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
                    await task;
                    
                    _logger.LogDebug("Handler {HandlerType} successfully processed event {EventType}", 
                        handler.GetType().Name, eventType.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error handling event {EventType} with handler {HandlerType}. Event ID: {EventId}", 
                    eventType.Name, handler.GetType().Name, domainEvent.EventId);
                
                // Don't throw - allow other handlers to execute
                // This ensures one failing handler doesn't prevent others from running
            }
        }

        _logger.LogDebug("Event published: {EventType} (ID: {EventId})", 
            eventType.Name, domainEvent.EventId);
    }
}
