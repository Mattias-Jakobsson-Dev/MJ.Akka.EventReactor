namespace MJ.Akka.EventReactor.PositionStreamSource;

public record PositionedStreamSettings(
    int Parallelism,
    int PositionBatchSize,
    TimeSpan PositionWriteInterval,
    bool UseDeadLetter,
    int MaxRetries);
