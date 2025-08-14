using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.DeadLetter;

public interface IDeadLetterManager
{
    Task<IImmutableList<DeadLetterData>> LoadDeadLetters();

    Task Retry(long to);

    Task Clear(long to);
}