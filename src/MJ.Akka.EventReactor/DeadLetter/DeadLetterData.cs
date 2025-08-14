using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.DeadLetter;

[PublicAPI]
public record DeadLetterData(
    long Position,
    object Message,
    IImmutableDictionary<string, object?> Metadata,
    string ErrorMessage);