using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public record EventReactorInstanceConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    IImmutableList<IOutputWriter> OutputWriters)
    : EventReactorConfig(RestartSettings, Parallelism, OutputWriters)
{
    public static EventReactorInstanceConfig Default { get; } 
        = new(null, null, ImmutableList<IOutputWriter>.Empty);

    public EventReactorInstanceConfig MergeWith(EventReactorSystemConfig systemConfig)
    {
        return this with
        {
            RestartSettings = RestartSettings ?? systemConfig.RestartSettings,
            Parallelism = Parallelism ?? systemConfig.Parallelism,
            OutputWriters = systemConfig.OutputWriters.AddRange(OutputWriters)
        };
    }
}