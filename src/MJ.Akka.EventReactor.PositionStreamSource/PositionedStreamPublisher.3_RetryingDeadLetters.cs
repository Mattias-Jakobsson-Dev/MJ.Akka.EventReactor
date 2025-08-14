using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.DeadLetter;

namespace MJ.Akka.EventReactor.PositionStreamSource;

public partial class PositionedStreamPublisher
{
    private readonly Queue<EventWithPosition> _deadLetterBuffer = new();
    private readonly Dictionary<IActorRef, long> _deadLetterDemand = new();
    
    private void RetryingDeadLetters()
    {
        var hasReceivedAllDeadLetters = false;
        
        Command<Commands.Request>(cmd =>
        {
            if (_deadLetterBuffer.Count > 0)
            {
                var currentDemand = cmd.Count;

                if (_deadLetterDemand.TryGetValue(Sender, out var value))
                    currentDemand += value;

                var events = new List<EventWithPosition>();

                while (currentDemand > 0 && _deadLetterBuffer.TryDequeue(out var evnt))
                {
                    events.Add(evnt);

                    currentDemand--;
                }

                if (events.Count != 0)
                    PushEventsTo(events.ToImmutableList(), Sender);

                if (currentDemand > 0)
                    _deadLetterDemand[Sender] = currentDemand;
                else
                    _deadLetterDemand.Remove(Sender);
            }
            else
            {
                _deadLetterDemand[Sender] = _deadLetterDemand.TryGetValue(Sender, out var value) ? value + cmd.Count : cmd.Count;
            }
        });

        Command<Commands.CancelDemand>(_ => { _deadLetterDemand.Remove(Sender); });

        Command<Commands.Ack>(cmd =>
        {
            Timers.Cancel($"timeout-{cmd.Position}");

            GetDeadLetter().Tell(new DeadLetterHandler.Commands.AckRetry(cmd.Position));
            
            if (_deadLetterBuffer.Count < 1 && hasReceivedAllDeadLetters)
            {
                foreach (var item in _deadLetterDemand)
                    item.Key.Tell(new Responses.CompletedRequestResponse());

                _deadLetterDemand.Clear();

                Become(Completed);
            }

            DeferAsync("done", _ => Sender.Tell(new Responses.AckNackResponse()));
        });

        Command<Commands.Nack>(cmd =>
        {
            Timers.Cancel($"timeout-{cmd.Position}");

            //TODO: Handle retries

            GetDeadLetter().Tell(new DeadLetterHandler.Commands.NackRetry(cmd.Position, cmd.Error));
            
            Sender.Tell(new Responses.AckNackResponse());
            
            if (_deadLetterBuffer.Count < 1 && hasReceivedAllDeadLetters)
            {
                foreach (var item in _deadLetterDemand)
                    item.Key.Tell(new Responses.CompletedRequestResponse());

                _deadLetterDemand.Clear();

                Become(Completed);
            }
        });

        Command<Commands.RetryDeadLetters>(_ =>
        {
            Sender.Tell(
                new Responses.RetryDeadLettersResponse(
                    new Exception("Can't retry dead letters when publisher is already retrying.")));
        });

        Command<Commands.ClearDeadLetters>(cmd =>
        {
            GetDeadLetter().Tell(new DeadLetterHandler.Commands.ClearDeadLetters(cmd.To));

            Sender.Tell(new Responses.ClearDeadLettersResponse());
        });

        Command<Commands.PushDeadLetter>(cmd =>
        {
            var sendTo = _deadLetterDemand
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .FirstOrDefault();

            if (sendTo != null)
            {
                PushEventsTo(
                    ImmutableList.Create(new EventWithPosition(
                        cmd.Message,
                        cmd.MetaData.ToImmutableDictionary(),
                        cmd.OriginalPosition)),
                    sendTo);

                _deadLetterDemand[sendTo]--;

                if (_deadLetterDemand[sendTo] <= 0)
                    _deadLetterDemand.Remove(sendTo);

                return;
            }

            _deadLetterBuffer.Enqueue(new EventWithPosition(
                cmd.Message,
                cmd.MetaData.ToImmutableDictionary(),
                cmd.OriginalPosition));
        });

        Command<Commands.LastDeadLetterPushed>(_ =>
        {
            hasReceivedAllDeadLetters = true;
        });
        
        Command<Queries.GetDeadLetters>(_ =>
        {
            var sender = Sender;

            GetDeadLetter()
                .Ask<DeadLetterHandler.Responses.GetResponse>(new DeadLetterHandler.Queries.Get())
                .ContinueWith(result =>
                {
                    sender.Tell(result.IsCompletedSuccessfully
                        ? new Responses.GetDeadLettersResponse(result.Result.DeadLetters)
                        : new Responses.GetDeadLettersResponse(ImmutableList<DeadLetterData>.Empty));
                });
        });
    }
}