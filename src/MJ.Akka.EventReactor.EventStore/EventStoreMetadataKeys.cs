namespace MJ.Akka.EventReactor.EventStore;

public static class EventStoreMetadataKeys
{
    public const string PersistenceId = nameof(PersistenceId);
    public const string SequenceNr = nameof(SequenceNr);
    public const string Manifest = nameof(Manifest);
    public const string Timestamp = nameof(Timestamp);
    public const string WriterGuid = nameof(WriterGuid);
    public const string Sender = nameof(Sender);
}