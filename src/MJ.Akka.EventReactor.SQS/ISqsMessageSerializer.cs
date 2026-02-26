using System.Collections.Immutable;
using Amazon.SQS.Model;

namespace MJ.Akka.EventReactor.SQS;

public interface ISqsMessageSerializer
{
    Task<SendMessageRequest> Serialize(object message);
    Task<(object data, IImmutableDictionary<string, object?> metadata)> DeSerialize(Message message);
}