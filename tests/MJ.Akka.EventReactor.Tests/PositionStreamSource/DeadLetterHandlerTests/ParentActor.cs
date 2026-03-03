using System.Collections.Generic;
using System.Collections.Immutable;
using Akka.Actor;
using MJ.Akka.EventReactor.PositionStreamSource;

namespace MJ.Akka.EventReactor.Tests.PositionStreamSource.DeadLetterHandlerTests;

/// <summary>
/// A simple parent actor that wraps a child (DeadLetterHandler) and collects
/// any PushDeadLetter messages forwarded to Context.Parent.
/// </summary>
public class ParentActor : ReceiveActor
{
    public record GetChild;
    public record GetCollectedMessages;

    private readonly List<PositionedStreamPublisher.Commands.PushDeadLetter> _collected = [];

    public ParentActor(Props childProps, IActorRef _)
    {
        var child = Context.ActorOf(childProps, "dlq");

        Receive<PositionedStreamPublisher.Commands.PushDeadLetter>(msg => _collected.Add(msg));
        Receive<GetChild>(_ => Sender.Tell(child));
        Receive<GetCollectedMessages>(_ =>
            Sender.Tell(_collected.ToImmutableList()));
    }
}

