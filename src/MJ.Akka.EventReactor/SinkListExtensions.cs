using System.Collections.Immutable;
using Akka;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace MJ.Akka.EventReactor;

public static class SinkListExtensions
{
    public static Sink<TOut, NotUsed> Combine<TOut, TMat>(
        this IEnumerable<Sink<TOut, NotUsed>> sinks,
        Func<int, IGraph<UniformFanOutShape<TOut, TOut>, TMat>> strategy)
    {
        var sinkList = sinks.ToImmutableList();

        return sinkList.Count switch
        {
            0 => throw new ArgumentException("At least one sink must be provided.", nameof(sinks)),
            1 => sinkList[0],
            2 => Sink.Combine(strategy, sinkList[0], sinkList[1]),
            _ => Sink.Combine(strategy, sinkList[0], sinkList[1], sinkList.Skip(2).ToArray())
        };
    }
}