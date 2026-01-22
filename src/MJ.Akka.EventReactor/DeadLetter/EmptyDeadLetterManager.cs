using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.DeadLetter;

public class EmptyDeadLetterManager : IDeadLetterManager
{
    public Task<IImmutableList<DeadLetterData>> LoadDeadLetters(long from, int count)
    {
        return Task.FromResult<IImmutableList<DeadLetterData>>(ImmutableList<DeadLetterData>.Empty);
    }

    public Task Retry(int count)
    {
        return Task.CompletedTask;
    }

    public Task Clear(long to)
    {
        return Task.CompletedTask;
    }
}