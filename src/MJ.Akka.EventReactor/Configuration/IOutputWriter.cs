using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor.Configuration;

public interface IOutputWriter
{
    //Flow<IPreparedForOutput, IPreparedForOutput, NotUsed> CreateWriter();
    IWriter CreateWriter();
    
    public interface IWriter
    {
        Task Write(IImmutableList<object> items, CancellationToken token);
    }
}
