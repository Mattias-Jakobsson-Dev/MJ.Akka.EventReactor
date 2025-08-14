using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Tests.TestData;

public interface ITestReactor : IEventReactor
{
    IImmutableDictionary<string, int> GetHandledEvents();
}