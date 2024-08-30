using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using DC.Akka.EventReactor.Configuration;

namespace DC.Akka.EventReactor;

public class EventReactorCoordinator : ReceiveActor
{
    public static class Commands
    {
        public record Start;

        public record Stop;

        public record Kill;

        public record WaitForCompletion;
    }

    private static class InternalCommands
    {
        public record Fail(Exception Cause);

        public record Complete;
    }

    public static class Responses
    {
        public record WaitForCompletionResponse(Exception? Error = null);

        public record StopResponse;
    }

    private readonly ILoggingAdapter _logger;

    private UniqueKillSwitch? _killSwitch;

    private readonly EventReactorConfiguration _configuration;

    private readonly HashSet<IActorRef> _waitingForCompletion = [];

    public EventReactorCoordinator(string reactorName)
    {
        _logger = Context.GetLogger();

        _configuration = Context
                             .System
                             .GetExtension<ReactorConfigurationsSupplier>()?
                             .GetConfigurationFor(reactorName) ??
                         throw new NoEventReactorException(reactorName);

        Become(Stopped);
    }

    private void Stopped()
    {
        Receive<Commands.Start>(_ =>
        {
            _logger.Info("Starting event reactor {0}", _configuration.Name);

            _killSwitch = MaybeCreateRestartSource(() =>
                {
                    _logger.Info("Starting event reactor source for {0}", _configuration.Name);

                    var cancellation = new CancellationTokenSource();

                    return _configuration
                        .StartSource()
                        .SelectAsyncUnordered(
                            100,
                            async msg =>
                            {
                                try
                                {
                                    await _configuration.Handle(msg.Message, cancellation.Token);

                                    await msg.Ack();
                                }
                                catch (Exception e)
                                {
                                    await msg.Nack(e);
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
                .ToMaterialized(Sink.ActorRef<NotUsed>(
                    Self,
                    new InternalCommands.Complete(),
                    ex => new InternalCommands.Fail(ex)), Keep.Left)
                .Run(Context.System.Materializer());

            Become(Started);
        });

        Receive<Commands.Kill>(_ => { Context.Stop(Self); });

        Receive<Commands.Stop>(_ => { Sender.Tell(new Responses.StopResponse()); });

        Receive<Commands.WaitForCompletion>(_ => { _waitingForCompletion.Add(Sender); });
    }

    private void Started()
    {
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

        Receive<Commands.Kill>(_ =>
        {
            _logger.Info("Killing projection {0}", _configuration.Name);

            _killSwitch?.Shutdown();

            Context.Stop(Self);
        });
    }

    private void Completed()
    {
        Receive<Commands.WaitForCompletion>(_ => { Sender.Tell(new Responses.WaitForCompletionResponse()); });

        Receive<Commands.Kill>(_ => { Context.Stop(Self); });

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

    protected override void PreStart()
    {
        Self.Tell(new Commands.Start());

        base.PreStart();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        _killSwitch?.Shutdown();

        Self.Tell(new Commands.Start());

        base.PreRestart(reason, message);
    }

    protected override void PostStop()
    {
        _killSwitch?.Shutdown();

        base.PostStop();
    }

    private static Source<NotUsed, NotUsed> MaybeCreateRestartSource(
        Func<Source<NotUsed, NotUsed>> createSource,
        RestartSettings? restartSettings)
    {
        return restartSettings != null
            ? RestartSource.OnFailuresWithBackoff(createSource, restartSettings)
            : createSource();
    }
}