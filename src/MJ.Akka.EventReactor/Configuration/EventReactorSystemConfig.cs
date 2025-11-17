using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public record EventReactorSystemConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    IImmutableList<IOutputWriter> OutputWriters,
    IImmutableDictionary<string, (
        IConfigureEventReactor eventReactor,
        Func<EventReactorSystemConfig, EventReactorConfiguration> setup)> EventReactors)
    : EventReactorConfig(RestartSettings, Parallelism, OutputWriters)
{
    public static EventReactorSystemConfig Default { get; } = new(
        null,
        null,
        ImmutableList<IOutputWriter>.Empty, 
        ImmutableDictionary<string, (
            IConfigureEventReactor eventReactor,
            Func<EventReactorSystemConfig, EventReactorConfiguration> setup)>.Empty);
}