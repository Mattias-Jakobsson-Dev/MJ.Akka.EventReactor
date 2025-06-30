using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Setup;

public record EventReactorInstanceConfig(
    RestartSettings? RestartSettings,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task>>, IReactToEvent>? CreateHandler)
    : EventReactorConfig(RestartSettings, CreateHandler)
{
    public static EventReactorInstanceConfig Default { get; } = new(null, null);

    public EventReactorInstanceConfig MergeWith(EventReactorSystemConfig systemConfig)
    {
        return this with
        {
            RestartSettings = RestartSettings ?? systemConfig.RestartSettings,
            CreateHandler = CreateHandler ?? systemConfig.CreateHandler
        };
    }
}