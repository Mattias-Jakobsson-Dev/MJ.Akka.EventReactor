using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public record EventReactorInstanceConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task>>, IReactToEvent>? CreateHandler)
    : EventReactorConfig(RestartSettings, Parallelism, CreateHandler)
{
    public static EventReactorInstanceConfig Default { get; } = new(null, null, null);

    public EventReactorInstanceConfig MergeWith(EventReactorSystemConfig systemConfig)
    {
        return this with
        {
            RestartSettings = RestartSettings ?? systemConfig.RestartSettings,
            CreateHandler = CreateHandler ?? systemConfig.CreateHandler,
            Parallelism = Parallelism ?? systemConfig.Parallelism
        };
    }
}