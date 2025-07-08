using System.Collections.Immutable;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public record EventWithPosition(object Event, IImmutableDictionary<string, object?> Metadata, long Position);