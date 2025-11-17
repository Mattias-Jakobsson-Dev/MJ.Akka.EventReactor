using MJ.Akka.EventReactor.Stateful;

namespace MJ.Akka.EventReactor.Tests.StateStorageTests;

public class InMemoryStatefulReactorStorageTests : StateStorageTestsBase
{
    protected override IStatefulReactorStorage CreateStorage()
    {
        return new InMemoryStatefulReactorStorage();
    }
}