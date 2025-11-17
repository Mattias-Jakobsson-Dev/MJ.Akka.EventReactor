using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Stateful;

[PublicAPI]
public static class StatefulEventReactorReactWithExtensions
{
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Action<TEvent> handler) => setup.ReactWith((_, evnt) =>
    {
        handler(evnt);

        return Task.CompletedTask;
    });
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Action<TState?, TEvent> handler) => setup.ReactWith((state, evnt) =>
    {
        handler(state, evnt);

        return Task.CompletedTask;
    });
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Action<TEvent, IImmutableDictionary<string, object?>> handler) => setup.ReactWith((_, evnt, metadata) =>
    {
        handler(evnt, metadata);

        return Task.CompletedTask;
    });
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Action<TState?, TEvent, IImmutableDictionary<string, object?>> handler) => setup.ReactWith((state, evnt, metadata) =>
    {
        handler(state, evnt, metadata);

        return Task.CompletedTask;
    });
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, Task> handler) => setup.ReactWith((_, evnt, _, _) => handler(evnt));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, Task> handler) => setup.ReactWith((state, evnt, _, _) => handler(state, evnt));

    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, Task> handler) => 
        setup.ReactWith((_, evnt, metadata, _) => handler(evnt, metadata));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, Task> handler) => 
        setup.ReactWith((state, evnt, metadata, _) => handler(state, evnt, metadata));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, CancellationToken, Task> handler) => setup.ReactWith((_, evnt, _, cancellationToken) 
        => handler(evnt, cancellationToken));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, CancellationToken, Task> handler) => setup.ReactWith((state, evnt, _, cancellationToken) 
        => handler(state, evnt, cancellationToken));

    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task> handler)
        => setup.ReactWith((_, evnt, metadata, token) => handler(evnt, metadata, token));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ReactWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task> handler)
    {
        return setup
            .HandleWith(async (context, token) =>
            {
                await handler(context.State, (TEvent)context.Event, context.Metadata, token);

                return ImmutableList<object>.Empty;
            });
    }
}