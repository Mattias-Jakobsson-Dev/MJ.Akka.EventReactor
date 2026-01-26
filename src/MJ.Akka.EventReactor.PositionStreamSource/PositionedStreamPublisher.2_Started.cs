using System.Collections.Immutable;
using Akka.Actor;
using Akka.Event;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public partial class PositionedStreamPublisher
{
    private void Started(CancellationTokenSource cancellation)
    {
        Command<InternalCommands.PushEvent>(cmd =>
        {
            if (_currentPosition != null && cmd.Event.Position < _currentPosition)
            {
                Sender.Tell(new InternalResponses.PushEventResponse());

                return;
            }
            
            _inFlightMessages[cmd.Event.Position] = (cmd.Event.Event, cmd.Event.Metadata.ToDictionary());

            var sendTo = _demand
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .FirstOrDefault();

            if (sendTo != null)
            {
                PushEventsTo(ImmutableList.Create(cmd.Event), sendTo);

                _demand[sendTo]--;

                if (_demand[sendTo] <= 0)
                    _demand.Remove(sendTo);

                Sender.Tell(new InternalResponses.PushEventResponse());

                return;
            }

            if (_buffer.Count < 100)
            {
                _buffer.Enqueue(cmd.Event);

                Sender.Tell(new InternalResponses.PushEventResponse());

                return;
            }

            Stash.Stash();
        });

        Command<Commands.Request>(cmd =>
        {
            if (_buffer.Count > 0)
            {
                var currentDemand = cmd.Count;

                if (_demand.TryGetValue(Sender, out var value))
                    currentDemand += value;

                var events = new List<EventWithPosition>();

                while (currentDemand > 0 && _buffer.TryDequeue(out var evnt))
                {
                    events.Add(evnt);

                    currentDemand--;
                }

                if (events.Count != 0)
                    PushEventsTo(events.ToImmutableList(), Sender);

                if (currentDemand > 0)
                    _demand[Sender] = currentDemand;
                else
                    _demand.Remove(Sender);

                Stash.UnstashAll();
            }
            else
            {
                Stash.UnstashAll();

                _demand[Sender] = _demand.TryGetValue(Sender, out var value) ? value + cmd.Count : cmd.Count;
            }
        });

        Command<Commands.CancelDemand>(_ => { _demand.Remove(Sender); });

        Command<Commands.Ack>(cmd =>
        {
            Timers.Cancel($"timeout-{cmd.Position}");

            var hasQueuedPositionUpdate = false;

            if (_positionsFromDeadLetters.Contains(cmd.Position))
            {
                GetDeadLetter().Tell(new DeadLetterHandler.Commands.AckRetry(cmd.Position));

                _positionsFromDeadLetters.Remove(cmd.Position);
            }
            else
            {
                _inFlightMessages.Remove(cmd.Position);

                _positionUpdatedQueue?.OfferAsync(new InternalCommands.WritePosition(ImmutableList.Create(cmd.Position)));

                hasQueuedPositionUpdate = true;
            }

            if (_inFlightMessages.Count == 0 && _positionsFromDeadLetters.Count == 0 && _shouldComplete && !hasQueuedPositionUpdate)
                CompleteGraph(cancellation);

            DeferAsync("done", _ => Sender.Tell(new Responses.AckNackResponse()));
        });

        Command<InternalCommands.WritePosition>(cmd =>
        {
            var positions = cmd.Positions
                .Where(x => !_inFlightMessages.Any(y => y.Key < x))
                .ToImmutableList();

            if (!positions.IsEmpty)
            {
                var position = positions.Max();

                if (_currentPosition == null || position > _currentPosition)
                {
                    Persist(new Events.PositionUpdated(position), On);

                    if (LastSequenceNr % 10 == 0 && LastSequenceNr > 0)
                        DeleteMessages(LastSequenceNr - 5);
                }
            }

            DeferAsync("done", _ =>
            {
                if (_inFlightMessages.Count == 0 && _positionsFromDeadLetters.Count == 0 && _shouldComplete)
                    CompleteGraph(cancellation);
            });
        });

        Command<Commands.Nack>(cmd =>
        {
            Timers.Cancel($"timeout-{cmd.Position}");

            //TODO: Handle retries

            if (_positionsFromDeadLetters.Contains(cmd.Position))
            {
                GetDeadLetter().Tell(new DeadLetterHandler.Commands.NackRetry(cmd.Position, cmd.Error));

                _positionsFromDeadLetters.Remove(cmd.Position);
                
                Sender.Tell(new Responses.AckNackResponse());
            }
            else
            {
                if (!_inFlightMessages.TryGetValue(cmd.Position, out var message))
                {
                    Sender.Tell(new Responses.AckNackResponse());

                    return;
                }

                var self = Self;
                var sender = Sender;

                GetDeadLetter().Ask<DeadLetterHandler.Responses.AddDeadLetterResponse>(
                        new DeadLetterHandler.Commands.AddDeadLetter(cmd.Position, message.message, message.metadata,
                            cmd.Error))
                    .ContinueWith(result =>
                    {
                        if (result.IsCompletedSuccessfully)
                            self.Tell(new Commands.Ack(cmd.Position), sender);
                        else
                            self.Tell(cmd, sender);
                    });
            }
        });

        Command<Commands.RetryDeadLetters>(cmd =>
        {
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.RetryDeadLetters(cmd.Count));

            Sender.Tell(new Responses.RetryDeadLettersResponse());
        });

        Command<Commands.ClearDeadLetters>(cmd =>
        {
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.ClearDeadLetters(cmd.To));

            Sender.Tell(new Responses.ClearDeadLettersResponse());
        });

        Command<Commands.PushDeadLetter>(cmd =>
        {
            if (_positionsFromDeadLetters.Contains(cmd.OriginalPosition))
                return;

            var sendTo = _demand
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .FirstOrDefault();

            if (sendTo != null)
            {
                _positionsFromDeadLetters.Add(cmd.OriginalPosition);

                PushEventsTo(
                    ImmutableList.Create(new EventWithPosition(
                        cmd.Message,
                        cmd.MetaData.ToImmutableDictionary(),
                        cmd.OriginalPosition)),
                    sendTo);

                _demand[sendTo]--;

                if (_demand[sendTo] <= 0)
                    _demand.Remove(sendTo);

                return;
            }

            if (_buffer.Count < 100)
            {
                _buffer.Enqueue(new EventWithPosition(
                    cmd.Message,
                    cmd.MetaData.ToImmutableDictionary(),
                    cmd.OriginalPosition));

                return;
            }

            Stash.Stash();
        });

        Command<InternalCommands.Completed>(_ =>
        {
            if (_inFlightMessages.Count != 0)
                _shouldComplete = true;
            else
                CompleteGraph(cancellation);
        });

        Command<InternalCommands.Failed>(cmd =>
        {
            cancellation.Cancel();
            
            Log.Error(cmd.Failure, "Positioned stream source failed for {0}", _eventReactorName);

            Become(() => Failed(cmd.Failure));
        });
    }
}