using Akka.Actor;

namespace MJ.Akka.EventReactor.Tests;

public interface IHaveActorSystem
{
    ActorSystem StartNewActorSystem();
}