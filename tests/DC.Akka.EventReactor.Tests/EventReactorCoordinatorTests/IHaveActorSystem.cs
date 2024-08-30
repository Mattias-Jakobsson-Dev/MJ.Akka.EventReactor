using Akka.Actor;

namespace DC.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public interface IHaveActorSystem
{
    ActorSystem StartNewActorSystem();
}