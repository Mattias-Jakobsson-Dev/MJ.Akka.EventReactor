using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public abstract record EventReactorConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    IImmutableList<IOutputWriter> OutputWriters,
    Func<IImmutableDictionary<Type, Func<object, CancellationToken, Task<IImmutableList<object>>>>, IReactToEvent>? CreateHandler);