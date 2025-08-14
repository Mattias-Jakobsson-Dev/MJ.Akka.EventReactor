using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using MJ.Akka.EventReactor.Configuration;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator : ReceiveActor
{
    public static class Commands
    {
        public record Start;

        public record Stop;

        public record WaitForCompletion;

        public record GetDeadLetters;

        public record RetryDeadLetters(long To);
        
        public record ClearDeadLetters(long To);
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

        public record GetDeadLettersResponse(IImmutableList<DeadLetterData> DeadLetters, Exception? Error = null);

        public record RetryDeadLetterResponse(Exception? Error = null);
        
        public record ClearDeadLetterResponse(Exception? Error = null);
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