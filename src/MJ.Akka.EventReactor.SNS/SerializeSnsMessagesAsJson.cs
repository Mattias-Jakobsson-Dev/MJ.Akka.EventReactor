using System.Text.Json;
using Amazon.SimpleNotificationService.Model;

namespace MJ.Akka.EventReactor.SNS;

public class SerializeSnsMessagesAsJson : ISnsMessageSerializer
{
    private const string TypeMetadataKey = "message-type";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<PublishRequest> Serialize(object message)
    {
        var json = JsonSerializer.Serialize(message, message.GetType(), Options);

        return Task.FromResult(new PublishRequest
        {
            Message = json,
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
}