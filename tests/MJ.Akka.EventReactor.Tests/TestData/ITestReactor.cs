using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Tests.TestData;

public interface ITestReactor : IConfigureEventReactor
{
    IImmutableDictionary<string, int> GetHandledEvents();
}