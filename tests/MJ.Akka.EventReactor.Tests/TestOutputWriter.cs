using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Akka;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.Tests;

public class TestOutputWriter : IOutputWriter
{
    private readonly Writer _writer = new();
    
    public Sink<IImmutableList<object>, NotUsed> CreateSink()
    {
        return Sink.FromWriter(_writer, true);
    }
    
    public IImmutableList<object> GetItems()
    {
        return _writer.GetItems();
    }
    
    private class Writer : ChannelWriter<IImmutableList<object>>
    {
        private readonly ConcurrentBag<object> _items = [];
        
        public override bool TryWrite(IImmutableList<object> item)
        {
            foreach (var result in item)
            {
                _items.Add(result);
            }

            return true;
        }

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = new())
        {
            return ValueTask.FromResult(true);
        }
        
        public IImmutableList<object> GetItems()
        {
            return _items.ToImmutableList();
        }
    }
}