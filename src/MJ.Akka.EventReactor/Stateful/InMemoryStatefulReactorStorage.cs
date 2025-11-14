using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MJ.Akka.EventReactor.Stateful;

public class InMemoryStatefulReactorStorage : IStatefulReactorStorage
{
    private readonly ConcurrentDictionary<string, ReadOnlyMemory<byte>> _states = new();
    
    public Task<TState?> Load<TState>(string id, CancellationToken cancellationToken)
    {
        return _states.TryGetValue(id, out var value) 
            ? Task.FromResult(DeserializeData<TState>(value)) 
            : Task.FromResult<TState?>(default);
    }

    public async Task Save<TState>(string id, TState state, CancellationToken cancellationToken)
    {
        var serialized = await SerializeData(state!);
        
        _states.AddOrUpdate(id, _ => serialized, (_, _) => serialized);
    }

    public Task Delete(string id, CancellationToken cancellationToken)
    {
        _states.TryRemove(id, out _);

        return Task.CompletedTask;
    }
    
    private static TState? DeserializeData<TState>(ReadOnlyMemory<byte> data)
    {
        return JsonSerializer.Deserialize<TState>(data.Span);
    }
    
    private static async Task<ReadOnlyMemory<byte>> SerializeData(object data)
    {
        var buffer = new ArrayBufferWriter<byte>();
        await using var writer = new Utf8JsonWriter(buffer);

        JsonSerializer.Serialize(writer, data);

        return buffer.WrittenMemory;
    }
}