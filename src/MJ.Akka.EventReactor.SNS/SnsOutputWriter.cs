using Akka;
using Akka.Streams.Dsl;
using Akka.Streams.Sns;
using Amazon.SimpleNotificationService;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.SNS;

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
                x => serializerToUse.Serialize(x))
            .ToMaterialized(SnsPublisher.PlainSink(topicArn, snsService), Keep.Left);
    }
}