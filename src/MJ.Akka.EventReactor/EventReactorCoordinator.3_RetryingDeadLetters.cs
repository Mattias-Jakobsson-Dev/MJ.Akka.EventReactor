using System.Collections.Immutable;
using Akka.Actor;
using Akka.Event;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator
{
    private void RetryingDeadLetters(IEventReactorEventSource source, Action previousState)
    {
        var deadLetterManager = (source as IEventReactorEventSourceWithDeadLetters)?.GetDeadLetters() ??
                                new EmptyDeadLetterManager();
        
        Receive<Commands.WaitForCompletion>(_ => { _waitingForCompletion.Add(Sender); });
        
        Receive<Commands.Stop>(_ =>
        {
            _logger.Info("Stopping event reactor {0}", _configuration.Name);

            HandleCompletionWaiters();

            Become(Stopped);

            Sender.Tell(new Responses.StopResponse());
        });
        
        ReceiveAsync<Commands.GetDeadLetters>(async _ =>
        {
            try
            {
                var deadLetters = await deadLetterManager.LoadDeadLetters();

                Sender.Tell(new Responses.GetDeadLettersResponse(deadLetters));
            }
            catch (Exception e)
            {
                Sender.Tell(new Responses.GetDeadLettersResponse(ImmutableList<DeadLetterData>.Empty, e));
            }
        });

        ReceiveAsync<Commands.RetryDeadLetters>(async cmd =>
        {
            try
            {
                await deadLetterManager.Retry(cmd.To);
                
                Sender.Tell(new Responses.RetryDeadLetterResponse());
            }
            catch (Exception e)
            {
                Sender.Tell(new Responses.RetryDeadLetterResponse(e));
            }
        });

        ReceiveAsync<Commands.ClearDeadLetters>(async cmd =>
        {
            try
            {
                await deadLetterManager.Clear(cmd.To);
                
                Sender.Tell(new Responses.ClearDeadLetterResponse());
            }
            catch (Exception e)
            {
                Sender.Tell(new Responses.ClearDeadLetterResponse(e));
            }
        });
        
        Receive<InternalCommands.Complete>(_ =>
        {
            HandleCompletionWaiters();

            Become(() => Completed(source));
        });
        
        Receive<InternalCommands.Fail>(cmd =>
        {
            _logger.Error(cmd.Cause, "Event reactor {0} failed", _configuration.Name);

            _killSwitch?.Shutdown();

            HandleCompletionWaiters(cmd.Cause);

            Become(previousState);
        });
    }
}