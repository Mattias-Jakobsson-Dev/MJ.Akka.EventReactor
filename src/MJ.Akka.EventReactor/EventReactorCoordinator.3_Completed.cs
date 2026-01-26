using Akka.Actor;

namespace MJ.Akka.EventReactor;

public partial class EventReactorCoordinator
{
    private void Completed()
    {
        Receive<Commands.WaitForCompletion>(_ => { Sender.Tell(new Responses.WaitForCompletionResponse()); });
        
        Receive<Commands.Stop>(_ =>
        {
            Become(Stopped);

            Sender.Tell(new Responses.StopResponse());
        });
    }
}