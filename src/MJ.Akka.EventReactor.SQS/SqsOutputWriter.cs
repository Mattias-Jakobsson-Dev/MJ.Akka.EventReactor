using System.Collections.Immutable;
using Amazon.SQS;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.SQS;

[PublicAPI]
public class SqsOutputWriter(
    IAmazonSQS client,
    string queueUrl,
    ISqsMessageSerializer? serializer = null) : IOutputWriter
{
    public IOutputWriter.IWriter CreateWriter()
    {
        return new Writer(client, queueUrl, serializer ?? new SerializeSqsMessagesAsJson());
    }

    private class Writer(
        IAmazonSQS client,
        string queueUrl,
        ISqsMessageSerializer serializer) : IOutputWriter.IWriter
    {
        public async Task Write(IImmutableList<object> items, CancellationToken token)
        {
            var requests = await Task.WhenAll(items.Select(x => serializer.Serialize(x)));

            foreach (var request in requests)
            {
                request.QueueUrl = queueUrl;
                await client.SendMessageAsync(request, token);
            }
        }
    }
}