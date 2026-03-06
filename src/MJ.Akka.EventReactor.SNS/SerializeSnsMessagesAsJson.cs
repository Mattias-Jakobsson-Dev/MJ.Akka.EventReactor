using System.Text.Json;

namespace MJ.Akka.EventReactor.SNS;

public class SerializeSnsMessagesAsJson : ISnsMessageSerializer
{
    public Task<string> Serialize(object message)
    {
        var json = JsonSerializer.Serialize(message);
        return Task.FromResult(json);
    }
}