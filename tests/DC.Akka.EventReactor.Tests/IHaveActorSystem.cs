using Akka.Actor;

namespace DC.Akka.EventReactor.Tests;

public interface IHaveActorSystem
{
    ActorSystem StartNewActorSystem();
}