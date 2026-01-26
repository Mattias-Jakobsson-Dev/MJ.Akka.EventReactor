using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.RavenDb;
using MJ.Akka.EventReactor.Tests.StateStorageTests;
using MJ.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public class StatefulReactorWithRavenDbPersistenceTests(
    NormalTestKitActorSystem systemHandler,
    RavenDbFixture ravenDbFixture)
    : EventReactorCoordinatorTestsBase(systemHandler),
        IClassFixture<NormalTestKitActorSystem>, IClassFixture<RavenDbFixture>
{
    protected override ITestReactor CreateReactor(
        IImmutableList<(Events.IEvent, IImmutableDictionary<string, object?>)> events,
        ActorSystem actorSystem,
        string? name = null)
    {
        return new StatefulTestReactor(
            actorSystem,
            new RavenDbReactorStateStorage(ravenDbFixture.OpenDocumentStore()),
            events,
            name);
    }
}