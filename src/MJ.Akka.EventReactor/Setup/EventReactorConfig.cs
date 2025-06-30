using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Setup;

public abstract record EventReactorConfig(
    RestartSettings? RestartSettings,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task>>, IReactToEvent>? CreateHandler);