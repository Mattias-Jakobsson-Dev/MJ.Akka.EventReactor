using System.Collections.Immutable;

namespace DC.Akka.EventReactor.Tests.TestData;

public interface ITestReactor : IEventReactor
{
    IImmutableDictionary<string, int> GetHandledEvents();
}