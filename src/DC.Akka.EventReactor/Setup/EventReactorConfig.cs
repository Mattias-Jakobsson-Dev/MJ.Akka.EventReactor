using System.Collections.Immutable;
using Akka.Streams;

namespace DC.Akka.EventReactor.Setup;

public abstract record EventReactorConfig(
    RestartSettings? RestartSettings,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task>>, IReactToEvent>? CreateHandler);