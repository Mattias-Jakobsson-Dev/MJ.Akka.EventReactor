using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public record EventReactorSystemConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task>>, IReactToEvent> CreateHandler,
    IImmutableDictionary<string, (
        IEventReactor eventReactor,
        Func<EventReactorSystemConfig, EventReactorConfiguration> setup)> EventReactors)
    : EventReactorConfig(RestartSettings, Parallelism, CreateHandler)
{
    public static EventReactorSystemConfig Default { get; } = new(
        null,
        null,
        handlers => new ReactToEventsInProcess(handlers),
        ImmutableDictionary<string, (
            IEventReactor eventReactor,
            Func<EventReactorSystemConfig, EventReactorConfiguration> setup)>.Empty);
}