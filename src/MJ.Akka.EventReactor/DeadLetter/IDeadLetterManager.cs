using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.DeadLetter;

public interface IDeadLetterManager
{
    Task<IImmutableList<DeadLetterData>> LoadDeadLetters(long from, int count);

    Task Retry(int count);

    Task Clear(long to);
}