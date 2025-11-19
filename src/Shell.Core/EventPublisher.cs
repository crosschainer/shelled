using Shell.Core.Interfaces;
using System.Collections.Concurrent;

namespace Shell.Core;

/// <summary>
/// Simple in-memory event publisher implementation
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _lock = new object();

    public void Publish<T>(T eventData) where T : class
    {
        if (eventData == null)
            return;

        var eventType = typeof(T);
        if (!_subscribers.TryGetValue(eventType, out var handlers))
            return;

        List<Delegate> handlersCopy;
        lock (_lock)
        {
            handlersCopy = new List<Delegate>(handlers);
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                if (handler is Action<T> typedHandler)
                {
                    typedHandler(eventData);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't let one handler failure affect others
                // In a real implementation, this would use proper logging
                Console.WriteLine($"Error in event handler: {ex.Message}");
            }
        }
    }

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null)
            return;

        var eventType = typeof(T);
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _subscribers[eventType] = handlers;
            }

            handlers.Add(handler);
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null)
            return;

        var eventType = typeof(T);
        lock (_lock)
        {
            if (_subscribers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                
                // Clean up empty lists
                if (handlers.Count == 0)
                {
                    _subscribers.TryRemove(eventType, out _);
                }
            }
        }
    }
}