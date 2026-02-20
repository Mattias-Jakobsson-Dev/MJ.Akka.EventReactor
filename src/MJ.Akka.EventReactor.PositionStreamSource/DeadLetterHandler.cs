using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence;
using JetBrains.Annotations;

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

        [PublicAPI]
        public record RetryDeadLetters(int Count);
        
        [PublicAPI]
        public record ClearDeadLetters(long To);

        public record RetryPending;

        public record AckRetry(long Position);

        public record NackRetry(long Position, Exception Error);
    }

    public static class Responses
    {
        public record ManageDeadLettersResponse;
    }

    public static class Events
    {
        [PublicAPI]
        public record DeadLetterAdded(
            string ReactorName,
            object Event,
            Dictionary<string, object?>? Metadata,
            string ErrorMessage,
            string? StackTrace,
            long OriginalPosition);

        [PublicAPI]
        public record DeadLetterRetriedSuccessfully(string ReactorName, long Position);
        
        [PublicAPI]
        public record DeadLettersCleared(string ReactorName, long Position);

        [PublicAPI]
        public record RetryStarted(string ReactorName, long FromPosition, long ToPosition);
    }

    private readonly string _eventReactorName;
    private readonly Dictionary<long, Events.DeadLetterAdded> _deadLetters = new();
    
    private IImmutableList<(long retryPos, long from, long to)> _activeRetries = [];
    
    private long? _cleanupEventPosition;

    public IImmutableList<(long retryPos, long from, long to)> ActiveRetries => _activeRetries
        .Where(x => _deadLetters.Keys.Any(pos => pos >= x.from && pos <= x.to))
        .ToImmutableList();
    
    public IImmutableList<Events.DeadLetterAdded> DeadLettersToRetry => _deadLetters
        .Where(x => ActiveRetries.Any(r => x.Key >= r.from && x.Key <= r.to))
        .OrderBy(x => x.Key)
        .Select(x => x.Value)
        .ToImmutableList();

    public DeadLetterHandler(string eventReactorName)
    {
        _eventReactorName = eventReactorName;

        Recover<Events.DeadLetterAdded>(On);
        Recover<Events.DeadLetterRetriedSuccessfully>(On);
        Recover<Events.DeadLettersCleared>(On);
        Recover<Events.RetryStarted>(On);

        Command<Commands.AddDeadLetter>(cmd =>
        {
            Persist(new Events.DeadLetterAdded(
                _eventReactorName,
                cmd.Event,
                cmd.Metadata,
                cmd.Error.Message,
                cmd.Error.StackTrace,
                cmd.Position), evnt =>
            {
                On(evnt);

                Sender.Tell(new Responses.ManageDeadLettersResponse());
            });
        });

        Command<Commands.RetryDeadLetters>(cmd =>
        {
            var messagesToRetry = _deadLetters
                .OrderBy(x => x.Key)
                .Take(cmd.Count)
                .Select(x => new
                {
                    Message = x.Value,
                    Position = x.Key
                })
                .ToImmutableList();

            if (!messagesToRetry.IsEmpty)
            {
                Persist(new Events.RetryStarted(
                    _eventReactorName,
                    messagesToRetry.Min(x => x.Position),
                    messagesToRetry.Max(x => x.Position)), On);
            }

            foreach (var deadLetter in messagesToRetry)
            {
                Context.Parent.Tell(new PositionedStreamPublisher.Commands.PushDeadLetter(
                        deadLetter.Message.OriginalPosition,
                        deadLetter.Message.Event,
                        deadLetter.Message.Metadata ?? new Dictionary<string, object?>()));
            }
            
            DeferAsync("done", _ => Sender.Tell(new Responses.ManageDeadLettersResponse()));
        });

        Command<Commands.RetryPending>(_ =>
        {
            foreach (var item in DeadLettersToRetry)
            {
                Context.Parent.Tell(new PositionedStreamPublisher.Commands.PushDeadLetter(
                    item.OriginalPosition,
                    item.Event,
                    item.Metadata ?? new Dictionary<string, object?>()));
            }
            
            CleanupEvents();
        });
        
        Command<Commands.ClearDeadLetters>(cmd =>
        {
            var to = cmd.To > LastSequenceNr ? LastSequenceNr : cmd.To;
            
            Persist(new Events.DeadLettersCleared(_eventReactorName, to), evnt =>
            {
                On(evnt);
                
                CleanupEvents();

                Sender.Tell(new Responses.ManageDeadLettersResponse());
            });
        });

        Command<Commands.AckRetry>(cmd =>
        {
            Persist(new Events.DeadLetterRetriedSuccessfully(_eventReactorName, cmd.Position), On);
        });

        Command<Commands.NackRetry>(cmd =>
        {
            if (!_deadLetters.TryGetValue(cmd.Position, out var deadLetter))
                return;

            Persist(deadLetter with { ErrorMessage = cmd.Error.Message }, On);
        });
    }

    private void CleanupEvents()
    {
        var positionToClean = _deadLetters.Count != 0
            ? _deadLetters
                .Keys
                .Min() - 1
            : LastSequenceNr;
        
        if (ActiveRetries.Any() && ActiveRetries.Min(x => x.retryPos) - 1 < positionToClean)
            positionToClean = ActiveRetries.Min(x => x.retryPos) - 1;
        
        if (_cleanupEventPosition <= positionToClean)
            positionToClean = _cleanupEventPosition.Value - 1;

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
        var deadLettersToRemove = _deadLetters
            .Where(x => x.Value.OriginalPosition == evnt.Position)
            .ToImmutableList();

        foreach (var item in deadLettersToRemove)
            _deadLetters.Remove(item.Key);

        _activeRetries = _activeRetries
            .Where(x => _deadLetters.Keys.Any(pos => pos >= x.from && pos <= x.to))
            .ToImmutableList();
    }

    private void On(Events.DeadLettersCleared evnt)
    {
        var positionsToClear = _deadLetters
            .Where(x => x.Key <= evnt.Position)
            .Select(x => x.Key)
            .ToImmutableList();

        foreach (var position in positionsToClear)
            _deadLetters.Remove(position);
        
        var retriesToClear = _activeRetries
            .Where(x => x.to <= evnt.Position)
            .ToImmutableList();
        
        foreach (var retry in retriesToClear)
            _activeRetries = _activeRetries.Remove(retry);

        _cleanupEventPosition = LastSequenceNr;
    }

    private void On(Events.RetryStarted evnt)
    {
        _activeRetries = _activeRetries.Add((LastSequenceNr, evnt.FromPosition, evnt.ToPosition));
    }
}