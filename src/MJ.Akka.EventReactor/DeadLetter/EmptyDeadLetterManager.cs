using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.DeadLetter;

public class EmptyDeadLetterManager : IDeadLetterManager
{
    public Task<IImmutableList<DeadLetterData>> LoadDeadLetters()
    {
        return Task.FromResult<IImmutableList<DeadLetterData>>(ImmutableList<DeadLetterData>.Empty);
    }

    public Task Retry(long to)
    {
        return Task.CompletedTask;
    }

    public Task Clear(long to)
    {
        return Task.CompletedTask;
    }
}