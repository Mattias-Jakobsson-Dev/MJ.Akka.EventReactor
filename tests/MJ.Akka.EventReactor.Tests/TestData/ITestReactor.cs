using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor;

namespace MJ.Akka.EventReactor.Tests.TestData;

public interface ITestReactor : IEventReactor
{
    Task<IImmutableList<string>> GetDeadLetters(ActorSystem actorSystem);
    IImmutableDictionary<string, int> GetHandledEvents();
}