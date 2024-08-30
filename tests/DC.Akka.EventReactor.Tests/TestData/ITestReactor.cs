using System.Collections.Immutable;
using Akka.Actor;

namespace DC.Akka.EventReactor.Tests.TestData;

public interface ITestReactor : IEventReactor
{
    Task<IImmutableList<string>> GetDeadLetters(ActorSystem actorSystem);
    IImmutableDictionary<string, int> GetHandledEvents();
}