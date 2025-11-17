using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Stateful;

[PublicAPI]
public static class StatefulEventReactorModifyStateExtensions
{
    public static ISetupStatefulEventReactorFor<TEvent, TState> ModifyState<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, TState?> handler) => setup.ModifyState((state, evnt) => Task.FromResult(handler(state, evnt)));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ModifyState<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, TState?> handler) => 
        setup.ModifyState((state, evnt, metadata, _) => Task.FromResult(handler(state, evnt, metadata)));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ModifyState<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, Task<TState?>> handler) => setup.ModifyState((state, evnt, _, _) => handler(state, evnt));

    public static ISetupStatefulEventReactorFor<TEvent, TState> ModifyState<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, CancellationToken, Task<TState?>> handler) =>
        setup.ModifyState((state, evnt, _, token) => handler(state, evnt, token));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ModifyState<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, Task<TState?>> handler) =>
        setup.ModifyState((state, evnt, metadata, _) => handler(state, evnt, metadata));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> ModifyState<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task<TState?>> handler) 
        => setup
            .HandleWith(async (context, token) =>
            {
                var newState = await handler(context.State, (TEvent)context.Event, context.Metadata, token);
                
                context.ModifyState(newState);
                
                return ImmutableList<object>.Empty;
            });
}