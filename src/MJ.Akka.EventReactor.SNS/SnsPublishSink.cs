using Akka;
using Akka.Streams.Dsl;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace MJ.Akka.EventReactor.SNS;

public static class SnsPublishSink
{
    public static Sink<PublishRequest, NotUsed> MessageSink(IAmazonSimpleNotificationService snsService)
    {
        return Flow.Create<PublishRequest>()
            .SelectAsync(1, request => snsService.PublishAsync(request))
            .ToMaterialized(Sink.Ignore<PublishResponse>(), Keep.Left);
    }
}