using Akka.Event;
using Akka.Actor;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator
{
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
}