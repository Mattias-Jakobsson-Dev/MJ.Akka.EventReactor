using MJ.Akka.EventReactor.RavenDb;
using MJ.Akka.EventReactor.Stateful;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.StateStorageTests;

public class RavenDbReactorStateStorageTests(RavenDbFixture fixture) : StateStorageTestsBase, IClassFixture<RavenDbFixture>
{
    protected override IStatefulReactorStorage CreateStorage()
    {
        return new RavenDbReactorStateStorage(fixture.OpenDocumentStore());
    }
}