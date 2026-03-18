using Akka;
using Akka.Streams.Dsl;
using Amazon.SimpleNotificationService;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.SNS;

[PublicAPI]
public class SnsOutputWriter(
    string topicArn,
    IAmazonSimpleNotificationService snsService,
    ISnsMessageSerializer? serializer = null,
    int serializationParallelism = 1) : IOutputWriter
{
    public Sink<object, NotUsed> CreateSink()
    {
        var serializerToUse = serializer ?? new SerializeSnsMessagesAsJson();

        return Flow.Create<object>()
            .SelectAsync(
                serializationParallelism,
                async x =>
                {
                    var request = await serializerToUse.Serialize(x);
                    request.TopicArn = topicArn;
                    return request;
                })
            .ToMaterialized(SnsPublishSink.MessageSink(snsService), Keep.Left);
    }
}