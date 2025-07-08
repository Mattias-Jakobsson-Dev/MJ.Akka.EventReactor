using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public record EventReactorInstanceConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    IImmutableList<IOutputWriter> OutputWriters,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task<IImmutableList<object>>>>, IReactToEvent>? CreateHandler)
    : EventReactorConfig(RestartSettings, Parallelism, OutputWriters, CreateHandler)
{
    public static EventReactorInstanceConfig Default { get; } 
        = new(null, null, ImmutableList<IOutputWriter>.Empty, null);

    public EventReactorInstanceConfig MergeWith(EventReactorSystemConfig systemConfig)
    {
        return this with
        {
            RestartSettings = RestartSettings ?? systemConfig.RestartSettings,
            CreateHandler = CreateHandler ?? systemConfig.CreateHandler,
            Parallelism = Parallelism ?? systemConfig.Parallelism,
            OutputWriters = systemConfig.OutputWriters.AddRange(OutputWriters)
        };
    }
}