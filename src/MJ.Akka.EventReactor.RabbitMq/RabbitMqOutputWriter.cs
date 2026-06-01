using System.Collections.Immutable;
using Akka.Streams.Amqp.RabbitMq;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Configuration;
using RabbitMQ.Client;

namespace MJ.Akka.EventReactor.RabbitMq;

[PublicAPI]
public class RabbitMqOutputWriter(
    AmqpSinkSettings settings,
    IRabbitMqMessageSerializer? serializer = null) : IOutputWriter
{
    public IOutputWriter.IWriter CreateWriter()
    {
        return new Writer(settings, serializer ?? new SerializeRabbitMqMessagesAsJson());
    }

    private class Writer(AmqpSinkSettings settings, IRabbitMqMessageSerializer serializer)
        : IOutputWriter.IWriter, IAsyncDisposable
    {
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task Write(IImmutableList<object> items, CancellationToken token)
        {
            var channel = await GetOrCreateChannelAsync(token);

            var messages = await Task.WhenAll(items.Select(serializer.Serialize));

            foreach (var message in messages)
            {
                var props = message.Properties != null
                    ? new BasicProperties(message.Properties)
                    : new BasicProperties();

                await channel.BasicPublishAsync(
                    exchange: settings.Exchange ?? string.Empty,
                    routingKey: message.RoutingKey ?? settings.RoutingKey ?? string.Empty,
                    mandatory: message.Mandatory,
                    basicProperties: props,
                    body: message.Bytes.ToArray(),
                    cancellationToken: token);
            }
        }

        private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken token)
        {
            if (_channel is { IsOpen: true })
                return _channel;

            await _lock.WaitAsync(token);
            try
            {
                if (_channel is { IsOpen: true })
                    return _channel;

                var factory = CreateConnectionFactory(settings.ConnectionSettings);
                _connection = await factory.CreateConnectionAsync(token);
                _channel = await _connection.CreateChannelAsync(cancellationToken: token);

                return _channel;
            }
            finally
            {
                _lock.Release();
            }
        }

        private static ConnectionFactory CreateConnectionFactory(IAmqpConnectionSettings connectionSettings)
        {
            return connectionSettings switch
            {
                AmqpConnectionUri uri => new ConnectionFactory { Uri = uri.Uri },
                AmqpConnectionDetails details => new ConnectionFactory
                {
                    HostName = details.HostAndPortList.FirstOrDefault().Item1 ?? "localhost",
                    Port = details.HostAndPortList.FirstOrDefault().Item2,
                    UserName = details.Credentials?.Username ?? ConnectionFactory.DefaultUser,
                    Password = details.Credentials?.Password ?? ConnectionFactory.DefaultPass,
                    VirtualHost = details.VirtualHost ?? ConnectionFactory.DefaultVHost,
                    Ssl = details.Ssl ?? new SslOption(),
                    AutomaticRecoveryEnabled = details.AutomaticRecoveryEnabled ?? true,
                    NetworkRecoveryInterval = details.NetworkRecoveryInterval ?? TimeSpan.FromSeconds(5),
                    ClientProvidedName = details.ClientProvidedName
                },
                _ => new ConnectionFactory()
            };
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null) await _channel.DisposeAsync();
            if (_connection != null) await _connection.DisposeAsync();
        }
    }
}