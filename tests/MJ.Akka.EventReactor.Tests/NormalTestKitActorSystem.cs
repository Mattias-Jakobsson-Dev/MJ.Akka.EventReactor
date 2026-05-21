using Akka.Actor;
using Akka.TestKit.Xunit;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Tests;

[PublicAPI]
public class NormalTestKitActorSystem : TestKit, IHaveActorSystem
{
    public ActorSystem StartNewActorSystem()
    {
        return ActorSystem.Create(Sys.Name, DefaultConfig);
    }
}