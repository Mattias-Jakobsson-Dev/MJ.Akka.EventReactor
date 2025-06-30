using System.Collections.Immutable;
using Akka.Streams;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.Setup;

public record EventReactorSystemConfig(
    RestartSettings? RestartSettings,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task>>, IReactToEvent> CreateHandler,
    IImmutableDictionary<string, (
        IEventReactor eventReactor,
        Func<EventReactorSystemConfig, EventReactorConfiguration> setup)> EventReactors)
    : EventReactorConfig(RestartSettings, CreateHandler)
{
    public static EventReactorSystemConfig Default { get; } = new(
        null,
        handlers => new ReactToEventsInProcess(handlers),
        ImmutableDictionary<string, (
            IEventReactor eventReactor,
            Func<EventReactorSystemConfig, EventReactorConfiguration> setup)>.Empty);
}