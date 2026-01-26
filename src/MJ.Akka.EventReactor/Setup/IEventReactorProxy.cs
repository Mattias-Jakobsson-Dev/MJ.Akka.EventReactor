using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Setup;

public interface IEventReactorProxy
{
    [PublicAPI]
    Task Stop();
    
    Task WaitForCompletion(TimeSpan? timeout = null);
}