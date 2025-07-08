using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor;

public class EventReactorCoordinator : ReceiveActor
{
    public static class Commands
    {
        public record Start;

        public record Stop;

        public record WaitForCompletion;
    }

    private static class InternalCommands
    {
        public record Fail(Exception Cause);

        public record Complete;
    }

    public static class Responses
    {
        public record StartResponse;
        
        public record WaitForCompletionResponse(Exception? Error = null);

        public record StopResponse;
    }

    private readonly ILoggingAdapter _logger;

    private UniqueKillSwitch? _killSwitch;

    private readonly EventReactorConfiguration _configuration;

    private readonly HashSet<IActorRef> _waitingForCompletion = [];

    // ReSharper disable once MemberCanBePrivate.Global
    public EventReactorCoordinator(ISupplyReactorConfiguration configSupplier)
    {
        _logger = Context.GetLogger();

        _configuration = configSupplier.GetConfiguration();

        Become(Stopped);
    }

    private void Stopped()
    {
        ReceiveAsync<Commands.Start>(async _ =>
        {
            _logger.Info("Starting event reactor {0}", _configuration.Name);

            var source = await _configuration.GetSource();

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
                                        .Handle(msg.Message, cancellation.Token);

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

            Become(Started);

            Sender.Tell(new Responses.StartResponse());
        });
        
        Receive<Commands.Stop>(_ => { Sender.Tell(new Responses.StopResponse()); });

        Receive<Commands.WaitForCompletion>(_ => { _waitingForCompletion.Add(Sender); });
    }

    private void Started()
    {
        Receive<Commands.Start>(_ =>
        {
            Sender.Tell(new Responses.StartResponse());
        });
        
        Receive<Commands.Stop>(_ =>
        {
            _logger.Info("Stopping event reactor {0}", _configuration.Name);

            _killSwitch?.Shutdown();

            HandleCompletionWaiters();

            Become(Stopped);

            Sender.Tell(new Responses.StopResponse());
        });

        Receive<InternalCommands.Fail>(cmd =>
        {
            _logger.Error(cmd.Cause, "Event reactor {0} failed", _configuration.Name);

            _killSwitch?.Shutdown();

            HandleCompletionWaiters(cmd.Cause);

            Become(Stopped);
        });

        Receive<Commands.WaitForCompletion>(_ => { _waitingForCompletion.Add(Sender); });

        Receive<InternalCommands.Complete>(_ =>
        {
            HandleCompletionWaiters();

            Become(Completed);
        });
    }

    private void Completed()
    {
        Receive<Commands.WaitForCompletion>(_ => { Sender.Tell(new Responses.WaitForCompletionResponse()); });
        
        Receive<Commands.Stop>(_ =>
        {
            Become(Stopped);

            Sender.Tell(new Responses.StopResponse());
        });
    }

    private void HandleCompletionWaiters(Exception? error = null)
    {
        foreach (var item in _waitingForCompletion)
            item.Tell(new Responses.WaitForCompletionResponse(error));

        _waitingForCompletion.Clear();
    }
    
    protected override void PreRestart(Exception reason, object message)
    {
        _killSwitch?.Shutdown();
        
        base.PreRestart(reason, message);
    }

    protected override void PostStop()
    {
        _killSwitch?.Shutdown();

        base.PostStop();
    }

    public static Props Init(ISupplyReactorConfiguration configSupplier)
    {
        return Props.Create(() => new EventReactorCoordinator(configSupplier));
    }

    private static Source<object, NotUsed> MaybeCreateRestartSource(
        Func<Source<object, NotUsed>> createSource,
        RestartSettings? restartSettings)
    {
        return restartSettings != null
            ? RestartSource.OnFailuresWithBackoff(createSource, restartSettings)
            : createSource();
    }
}