using System.Collections.Immutable;
using MJ.Akka.EventReactor.DeadLetter;
using Akka.Actor;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator
{
    private void Completed(IEventReactorEventSource source)
    {
        var deadLetterManager = (source as IEventReactorEventSourceWithDeadLetters)?.GetDeadLetters() ??
                                new EmptyDeadLetterManager();
        
        Receive<Commands.WaitForCompletion>(_ => { Sender.Tell(new Responses.WaitForCompletionResponse()); });
        
        Receive<Commands.Stop>(_ =>
        {
            Become(Stopped);

            Sender.Tell(new Responses.StopResponse());
        });
        
        ReceiveAsync<Commands.GetDeadLetters>(async cmd =>
        {
            try
            {
                var deadLetters = await deadLetterManager.LoadDeadLetters(cmd.From, cmd.Count);

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
                await deadLetterManager.Retry(cmd.Count);

                StartSource(source);

                Become(() => RetryingDeadLetters(source, () => Completed(source)));

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
    }
}