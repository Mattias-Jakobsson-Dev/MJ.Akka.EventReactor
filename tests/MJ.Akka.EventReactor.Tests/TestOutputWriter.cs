using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Akka;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.Tests;

public class TestOutputWriter : IOutputWriter
{
    private readonly ConcurrentBag<object> _items = [];

    public Sink<object, NotUsed> CreateSink()
    {
        return Sink.FromWriter(new ChannelWriter(_items), true);
    }

    public IOutputWriter.IWriter CreateWriter()
    {
        return new Writer(_items);
    }

    public IImmutableList<object> GetItems()
    {
        return _items.ToImmutableList();
    }

    private class Writer(ConcurrentBag<object> items) : IOutputWriter.IWriter
    {
        public Task Write(IImmutableList<object> newItems, CancellationToken token)
        {
            foreach (var item in newItems)
                items.Add(item);

            return Task.CompletedTask;
        }
    }

    private class ChannelWriter(ConcurrentBag<object> items) : ChannelWriter<object>
    {
        public override bool TryWrite(object item)
        {
            items.Add(item);
            return true;
        }

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = new())
        {
            return ValueTask.FromResult(true);
        }
    }
}