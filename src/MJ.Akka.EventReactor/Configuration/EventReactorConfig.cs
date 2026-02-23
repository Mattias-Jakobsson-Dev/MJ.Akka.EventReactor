using System.Collections.Immutable;
using Akka.Streams;

namespace MJ.Akka.EventReactor.Configuration;

public abstract record EventReactorConfig(
    RestartSettings? RestartSettings,
    int? Parallelism,
    TimeSpan? Timeout,
    IImmutableList<IOutputWriter> OutputWriters);