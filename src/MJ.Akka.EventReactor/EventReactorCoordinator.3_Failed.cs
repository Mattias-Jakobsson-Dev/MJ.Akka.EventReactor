using Akka.Actor;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator
{
    private void Failed(Exception error)
    {
        Receive<Commands.Start>(cmd =>
        {
            Become(Stopped);
            
            Self.Tell(cmd, Sender);
        });
        
        Receive<Commands.WaitForCompletion>(_ => { Sender.Tell(new Responses.WaitForCompletionResponse(error)); });
        
        Receive<Commands.Stop>(_ =>
        {
            Become(Stopped);

            Sender.Tell(new Responses.StopResponse());
        });
    }
}