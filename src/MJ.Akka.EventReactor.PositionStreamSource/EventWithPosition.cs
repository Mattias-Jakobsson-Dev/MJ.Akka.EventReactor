namespace MJ.Akka.EventReactor.PositionStreamSource;

public record EventWithPosition(object Event, long Position);