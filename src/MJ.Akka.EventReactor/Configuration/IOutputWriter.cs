using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.Configuration;

public interface IOutputWriter
{
    Sink<IImmutableList<object>, NotUsed> CreateSink();
}