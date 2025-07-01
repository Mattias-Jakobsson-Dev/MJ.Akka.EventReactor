using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.Tests.TestData;

public interface ITestReactor : IEventReactor
{
    Task<IImmutableList<string>> GetDeadLetters();
    IImmutableDictionary<string, int> GetHandledEvents();
}