using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public record EventReactorSystemConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    IImmutableList<IOutputWriter> OutputWriters,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task<IImmutableList<object>>>>, IReactToEvent> CreateHandler,
    IImmutableDictionary<string, (
        IEventReactor eventReactor,
        Func<EventReactorSystemConfig, EventReactorConfiguration> setup)> EventReactors)
    : EventReactorConfig(RestartSettings, Parallelism, OutputWriters, CreateHandler)
{
    public static EventReactorSystemConfig Default { get; } = new(
        null,
        null,
        ImmutableList<IOutputWriter>.Empty, 
        handlers => new ReactToEventsInProcess(handlers),
        ImmutableDictionary<string, (
            IEventReactor eventReactor,
            Func<EventReactorSystemConfig, EventReactorConfiguration> setup)>.Empty);
}