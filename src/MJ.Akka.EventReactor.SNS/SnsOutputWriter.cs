using System.Collections.Immutable;
using Amazon.SimpleNotificationService;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.SNS;

[PublicAPI]
public class SnsOutputWriter(
    string topicArn,
    IAmazonSimpleNotificationService snsService,
    ISnsMessageSerializer? serializer = null) : IOutputWriter
{
    public IOutputWriter.IWriter CreateWriter()
    {
        return new Writer(topicArn, snsService, serializer ?? new SerializeSnsMessagesAsJson());
    }

    private class Writer(
        string topicArn,
        IAmazonSimpleNotificationService snsService,
        ISnsMessageSerializer serializer) : IOutputWriter.IWriter
    {
        public async Task Write(IImmutableList<object> items, CancellationToken token)
        {
            var requests = await Task.WhenAll(items.Select(serializer.Serialize));

            foreach (var request in requests)
            {
                request.TopicArn = topicArn;
                await snsService.PublishAsync(request, token);
            }
        }
    }
}