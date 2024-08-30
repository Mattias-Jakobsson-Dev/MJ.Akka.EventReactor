using Akka.Actor;
using Akka.TestKit.Xunit2;

namespace DC.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public class NormalTestKitActorSystem : TestKit, IHaveActorSystem
{
    public ActorSystem StartNewActorSystem()
    {
        return ActorSystem.Create(Sys.Name, DefaultConfig);
    }
}