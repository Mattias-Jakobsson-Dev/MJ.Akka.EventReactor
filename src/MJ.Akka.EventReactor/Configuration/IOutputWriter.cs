using Akka;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.Configuration;

public interface IOutputWriter
{
    Sink<object, NotUsed> CreateSink();
}