using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.Stateful;
using MJ.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public class StatefulReactorTests(NormalTestKitActorSystem systemHandler)
    : EventReactorCoordinatorTestsBase(systemHandler), IClassFixture<NormalTestKitActorSystem>
{
    protected override ITestReactor CreateReactor(
        IImmutableList<(Events.IEvent, IImmutableDictionary<string, object?>)> events,
        ActorSystem actorSystem,
        string? name = null)
    {
        return new StatefulTestReactor(
            actorSystem,
            new InMemoryStatefulReactorStorage(),
            events,
            name);
    }
}