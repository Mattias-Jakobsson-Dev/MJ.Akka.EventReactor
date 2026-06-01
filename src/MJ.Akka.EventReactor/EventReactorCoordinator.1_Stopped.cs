using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator
{
    private void StartSource(IEventReactorEventSource source)
    {
        var self = Self;
        
        _killSwitch = MaybeCreateRestartSource(() =>
            {
                _logger.Info("Starting event reactor source for {0}", _configuration.Name);

                var cancellation = new CancellationTokenSource();

                var outputWriters = _configuration
                    .OutputWriters
                    .Select(x => x.CreateWriter())
                    .ToImmutableList();

                return source
                    .Start()
                    .SelectAsyncUnordered(
                        _configuration.Parallelism,
                        async msg =>
                        {
                            using var timeoutToken = new CancellationTokenSource(_configuration.Timeout);

                            try
                            {
                                var result = await _configuration
                                    .Handle(msg, CancellationTokenSource.CreateLinkedTokenSource(
                                        cancellation.Token,
                                        timeoutToken.Token).Token)
                                    .WaitAsync(_configuration.Timeout, timeoutToken.Token);
                                
                                await Task.WhenAll(
                                    outputWriters
                                        .Select(x => x.Write(result, cancellation.Token)));

                                await msg.Ack(cancellation.Token);

                                return NotUsed.Instance;
                            }
                            catch (OperationCanceledException e)
                            {
                                Exception reason = e;

                                if (timeoutToken.IsCancellationRequested)
                                    reason = new TimeoutException($"Event handling timed out after {_configuration.Timeout}", e);

                                await msg.Nack(reason, cancellation.Token);
                            }
                            catch (Exception e)
                            {
                                await msg.Nack(e, cancellation.Token);
                            }

                            return NotUsed.Instance;
                        })
                    .Recover(_ =>
                    {
                        cancellation.Cancel();

                        return Option<NotUsed>.None;
                    });
            }, _configuration.RestartSettings)
            .ViaMaterialized(KillSwitches.Single<NotUsed>(), Keep.Right)
            .ToMaterialized(Sink.OnComplete<NotUsed>(
                () => self.Tell(new InternalCommands.Complete()),
                ex => self.Tell(new InternalCommands.Fail(ex))), Keep.Left)
            .Run(Context.System.Materializer());
    }

    private void Stopped()
    {
        ReceiveAsync<Commands.Start>(async _ =>
        {
            _logger.Info("Starting event reactor {0}", _configuration.Name);

            var source = await _configuration.GetSource();

            StartSource(source);

            Become(Started);

            Sender.Tell(new Responses.StartResponse());
        });

        Receive<Commands.Stop>(_ => { Sender.Tell(new Responses.StopResponse()); });

        Receive<Commands.WaitForCompletion>(_ => { _waitingForCompletion.Add(Sender); });
    }
}