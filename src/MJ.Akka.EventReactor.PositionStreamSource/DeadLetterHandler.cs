using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public class DeadLetterHandler : ReceivePersistentActor
{
    public static class Commands
    {
        public record AddDeadLetter(
            long Position,
            object Event,
            Dictionary<string, object?> Metadata,
            Exception Error);

        public record RetryDeadLetters(long To);
        
        public record ClearDeadLetters(long To);

        public record AckRetry(long Position);

        public record NackRetry(long Position, Exception Error);
    }

    public static class Queries
    {
        public record Get;
    }

    public static class Responses
    {
        public record AddDeadLetterResponse;

        public record GetResponse(IImmutableList<DeadLetterData> DeadLetters);
    }

    public static class Events
    {
        public record DeadLetterAdded(
            object Event,
            Dictionary<string, object?>? Metadata,
            string ErrorMessage,
            long OriginalPosition);

        public record DeadLetterRetriedSuccessfully(long Position);
        
        public record DeadLettersCleared(long Position);
    }

    private readonly string _eventReactorName;
    private readonly Dictionary<long, Events.DeadLetterAdded> _deadLetters = new();

    // ReSharper disable once MemberCanBePrivate.Global
    public DeadLetterHandler(string eventReactorName)
    {
        _eventReactorName = eventReactorName;

        Recover<Events.DeadLetterAdded>(On);
        Recover<Events.DeadLetterRetriedSuccessfully>(On);
        Recover<Events.DeadLettersCleared>(On);

        Command<Commands.AddDeadLetter>(cmd =>
        {
            Persist(new Events.DeadLetterAdded(cmd.Event, cmd.Metadata, cmd.Error.Message, cmd.Position), evnt =>
            {
                On(evnt);

                Sender.Tell(new Responses.AddDeadLetterResponse());
            });
        });

        Command<Commands.RetryDeadLetters>(cmd =>
        {
            var messagesToRetry = _deadLetters
                .Where(x => x.Key <= cmd.To)
                .Select(x => x.Value)
                .ToImmutableList();

            foreach (var deadLetter in messagesToRetry)
            {
                Context.Parent.Tell(new PositionedStreamPublisher.Commands.PushDeadLetter(
                        deadLetter.OriginalPosition,
                        deadLetter.Event,
                        deadLetter.Metadata ?? new Dictionary<string, object?>()));
            }
            
            Context.Parent.Tell(new PositionedStreamPublisher.Commands.LastDeadLetterPushed());
        });
        
        Command<Commands.ClearDeadLetters>(cmd =>
        {
            Persist(new Events.DeadLettersCleared(cmd.To), evnt =>
            {
                On(evnt);
                
                CleanupEvents();
            });
        });

        Command<Commands.AckRetry>(cmd =>
        {
            Persist(new Events.DeadLetterRetriedSuccessfully(cmd.Position), evnt =>
            {
                On(evnt);

                CleanupEvents();
            });
        });

        Command<Commands.NackRetry>(cmd =>
        {
            if (!_deadLetters.TryGetValue(cmd.Position, out var deadLetter))
                return;

            Persist(deadLetter with { ErrorMessage = cmd.Error.Message }, evnt =>
            {
                On(evnt);

                CleanupEvents();
            });
        });

        Command<Queries.Get>(_ =>
        {
            Sender.Tell(new Responses.GetResponse(_deadLetters
                .Select(x => new DeadLetterData(
                    x.Key,
                    x.Value.Event,
                    (x.Value.Metadata ?? new Dictionary<string, object?>()).ToImmutableDictionary(),
                    x.Value.ErrorMessage))
                .ToImmutableList()));
        });
    }

    private void CleanupEvents()
    {
        var positionToClean = _deadLetters.Count != 0
            ? _deadLetters
                .Keys
                .Min() - 1
            : LastSequenceNr;

        DeleteMessages(positionToClean);
    }

    public override string PersistenceId => $"event-reactor-dead-letters-{_eventReactorName}";

    public static Props Init(string eventReactorName)
    {
        return Props.Create(() => new DeadLetterHandler(eventReactorName));
    }

    private void On(Events.DeadLetterAdded evnt)
    {
        var earlierDeadLetters = _deadLetters
            .Where(x => x.Value.OriginalPosition == evnt.OriginalPosition)
            .ToImmutableList();

        foreach (var earlierDeadLetter in earlierDeadLetters)
            _deadLetters.Remove(earlierDeadLetter.Key);
        
        _deadLetters[LastSequenceNr] = evnt;
    }

    private void On(Events.DeadLetterRetriedSuccessfully evnt)
    {
        _deadLetters.Remove(evnt.Position);
    }

    private void On(Events.DeadLettersCleared evnt)
    {
        var positionsToClear = _deadLetters
            .Where(x => x.Key <= evnt.Position)
            .Select(x => x.Key)
            .ToImmutableList();

        foreach (var position in positionsToClear)
            _deadLetters.Remove(position);
    }
}