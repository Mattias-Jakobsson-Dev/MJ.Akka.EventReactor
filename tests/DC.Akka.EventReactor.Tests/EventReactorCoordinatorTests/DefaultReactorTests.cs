using System.Collections.Immutable;
using DC.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace DC.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public class DefaultReactorTests(NormalTestKitActorSystem systemHandler) 
    : EventReactorCoordinatorTestsBase(systemHandler), IClassFixture<NormalTestKitActorSystem>
{
    protected override ITestReactor CreateReactor(IImmutableList<Events.IEvent> events)
    {
        return new TestReactor(events);
    }
}