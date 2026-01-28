using System.Collections.Immutable;
using System.Diagnostics;
using Akka.Persistence;
using Akka.Persistence.Journal;
using Akka.Persistence.TestKit;

namespace MJ.Akka.EventReactor.Tests;

public class StoredEventsInterceptor : IJournalInterceptor
{
    private readonly List<StoredEvent> _storedEvents = [];

    public StoredEventsInterceptor()
    {
        EventStored += storedEvent => _storedEvents.Add(storedEvent);
    }

    public IImmutableList<StoredEvent> StoredEvents => _storedEvents.ToImmutableList();

    protected event Action<StoredEvent>? EventStored;

    public Task InterceptAsync(IPersistentRepresentation message)
    {
        var payload = message.Payload;

        while (true)
        {
            if (payload is Tagged tagged)
            {
                payload = tagged.Payload;
                continue;
            }

            break;
        }

        EventStored?.Invoke(new StoredEvent(message.PersistenceId, payload));

        return Task.CompletedTask;
    }

    public async Task<T> WaitForEvent<T>(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var storedEvent = StoredEvents.FirstOrDefault(x => x.Event is T);

            if (storedEvent != null)
            {
                return (T)storedEvent.Event;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Event of type {typeof(T).Name} was not stored within {timeout}");
    }

    public record StoredEvent(string PersistenceId, object Event);
}