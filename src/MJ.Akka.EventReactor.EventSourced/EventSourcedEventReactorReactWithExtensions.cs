using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.EventSourced;

[PublicAPI]
public static class EventSourcedEventReactorReactWithExtensions
{
    public static ISetupEventSourcedEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupEventSourcedEventReactorFor<TEvent, TState> setup,
        Func<TEvent, IEnumerable<object>> handler) => setup.ReactWith((_, evnt) => handler(evnt));

    public static ISetupEventSourcedEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupEventSourcedEventReactorFor<TEvent, TState> setup,
        Func<TState, TEvent, IEnumerable<object>> handler) => setup.ReactWith((state, evnt, _, _) =>
            Task.FromResult(handler(state, evnt)));

    public static ISetupEventSourcedEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupEventSourcedEventReactorFor<TEvent, TState> setup,
        Func<TState, TEvent, IImmutableDictionary<string, object?>, IEnumerable<object>> handler) =>
        setup.ReactWith((state, evnt, metadata, _) => Task.FromResult(handler(state, evnt, metadata)));

    public static ISetupEventSourcedEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupEventSourcedEventReactorFor<TEvent, TState> setup,
        Func<TEvent, Task<IEnumerable<object>>> handler) => setup.ReactWith((_, evnt, _, _) => handler(evnt));

    public static ISetupEventSourcedEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupEventSourcedEventReactorFor<TEvent, TState> setup,
        Func<TState, TEvent, Task<IEnumerable<object>>> handler) => setup.ReactWith((state, evnt, _, _) => handler(state, evnt));

    public static ISetupEventSourcedEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupEventSourcedEventReactorFor<TEvent, TState> setup,
        Func<TState, TEvent, IImmutableDictionary<string, object?>, Task<IEnumerable<object>>> handler) =>
        setup.ReactWith((state, evnt, metadata, _) => handler(state, evnt, metadata));

    public static ISetupEventSourcedEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupEventSourcedEventReactorFor<TEvent, TState> setup,
        Func<TState, TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task<IEnumerable<object>>> handler)
    {
        return setup
            .HandleWith(async (context, token) =>
            {
                var events = await handler(context.State, (TEvent)context.Event, context.Metadata, token);

                return events.ToImmutableList();
            });
    }
}
