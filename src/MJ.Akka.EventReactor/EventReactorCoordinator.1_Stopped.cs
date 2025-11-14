using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator
{
    private void StartSource(IEventReactorEventSource source)
    {
        var self = Self;

        var sinks = ImmutableList.Create<Sink<object, NotUsed>>(
                Sink.OnComplete<object>(
                    () => self.Tell(new InternalCommands.Complete()),
                    ex => self.Tell(new InternalCommands.Fail(ex))))
            .AddRange(_configuration.OutputWriters.Select(x => x.CreateSink()));

        _killSwitch = MaybeCreateRestartSource(() =>
            {
                _logger.Info("Starting event reactor source for {0}", _configuration.Name);

                var cancellation = new CancellationTokenSource();

                return source
                    .Start()
                    .SelectAsyncUnordered(
                        _configuration.Parallelism,
                        async msg =>
                        {
                            try
                            {
                                var result = await _configuration
                                    .Handle(msg, cancellation.Token);

                                await msg.Ack(cancellation.Token);

                                return result;
                            }
                            catch (Exception e)
                            {
                                await msg.Nack(e, cancellation.Token);
                            }

                            return ImmutableList<object>.Empty;
                        })
                    .Recover(_ =>
                    {
                        cancellation.Cancel();

                        return Option<IImmutableList<object>>.None;
                    })
                    .SelectMany(items => items);
            }, _configuration.RestartSettings)
            .ViaMaterialized(KillSwitches.Single<object>(), Keep.Right)
            .ToMaterialized(sinks.Combine(i => new Broadcast<object>(i)), Keep.Left)
            .Run(Context.System.Materializer());
    }

    private void Stopped()
    {
        ReceiveAsync<Commands.Start>(async _ =>
        {
            _logger.Info("Starting event reactor {0}", _configuration.Name);

            var source = await _configuration.GetSource();

            StartSource(source);

            Become(() => Started(source));

            Sender.Tell(new Responses.StartResponse());
        });

        Receive<Commands.Stop>(_ => { Sender.Tell(new Responses.StopResponse()); });

        Receive<Commands.WaitForCompletion>(_ => { _waitingForCompletion.Add(Sender); });

        Receive<Commands.GetDeadLetters>(_ =>
        {
            Sender.Tell(new Responses.GetDeadLettersResponse(
                ImmutableList<DeadLetterData>.Empty,
                new Exception("Can't get dead letters when event reactor is stopped.")));
        });

        Receive<Commands.RetryDeadLetters>(_ =>
        {
            Sender.Tell(
                new Responses.RetryDeadLetterResponse(
                    new Exception("Can't retry dead letter when event reactor is stopped.")));
        });

        Receive<Commands.ClearDeadLetters>(_ =>
        {
            Sender.Tell(
                new Responses.ClearDeadLetterResponse(
                    new Exception("Can't clear dead letter when event reactor is stopped.")));
        });
    }
}