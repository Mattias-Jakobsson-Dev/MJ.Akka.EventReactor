using System.Collections.Immutable;
using System.Text.Json;
using Amazon.SQS.Model;

namespace MJ.Akka.EventReactor.SQS;

public class SerializeSqsMessagesAsJson : ISqsMessageSerializer
{
    private const string TypeMetadataKey = "message-type";
    
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public Task<SendMessageRequest> Serialize(object message)
    {
        var json = JsonSerializer.Serialize(message, message.GetType(), Options);

        return Task.FromResult(new SendMessageRequest
        {
            MessageBody = json,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [TypeMetadataKey] = new()
                {
                    DataType = "String",
                    StringValue = message.GetType().AssemblyQualifiedName
                }
            }
        });
    }

    public Task<(object data, IImmutableDictionary<string, object?> metadata)> DeSerialize(Message message)
    {
        var json = message.Body;

        var typeName = message.MessageAttributes.TryGetValue(TypeMetadataKey, out var raw)
            ? raw.StringValue
            : null;

        var type = typeName != null
            ? Type.GetType(typeName) ?? throw new SerializationException($"Could not resolve type '{typeName}'")
            : typeof(JsonElement);

        var result = JsonSerializer.Deserialize(json, type, Options)
                     ?? throw new SerializationException("Failed to deserialize SQS message");
        
        return Task.FromResult<(object data, IImmutableDictionary<string, object?> metadata)>(
            (result, ImmutableDictionary<string, object?>.Empty));
    }
}