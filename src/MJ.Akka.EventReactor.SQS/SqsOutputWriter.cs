using Akka;
using Akka.Streams.Dsl;
using Akka.Streams.SQS;
using Amazon.SQS;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.SQS;

[PublicAPI]
public class SqsOutputWriter(
    IAmazonSQS client,
    string queueUrl,
    ISqsMessageSerializer? serializer = null,
    int serializationParallelism = 1,
    SqsPublishSettings? settings = null) : IOutputWriter
{
    public Sink<object, NotUsed> CreateSink()
    {
        var serializerToUse = serializer ?? new SerializeSqsMessagesAsJson();
        
        return Flow.Create<object>()
            .SelectAsync(
                serializationParallelism,
                async x =>
                {
                    var serialized = await serializerToUse.Serialize(x);

                    serialized.QueueUrl = queueUrl;

                    return serialized;
                })
            .ToMaterialized(SqsPublishSink.MessageSink(client, queueUrl, settings), Keep.Left);
    }
}