using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Stateful;

[PublicAPI]
public static class StatefulEventReactorTransformWithExtensions
{
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, IImmutableList<object>> handler) => setup.TransformWith((_, evnt) => Task.FromResult(handler(evnt)));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableList<object>> handler) => setup.TransformWith((state, evnt) => Task.FromResult(handler(state, evnt)));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, IImmutableList<object>> handler) => 
        setup.TransformWith((_, evnt, metadata, _) => Task.FromResult(handler(evnt, metadata)));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, IImmutableList<object>> handler) => 
        setup.TransformWith((state, evnt, metadata, _) => Task.FromResult(handler(state, evnt, metadata)));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, Task<IImmutableList<object>>> handler) => setup.TransformWith((_, evnt, _, _) => handler(evnt));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, Task<IImmutableList<object>>> handler) => setup.TransformWith((state, evnt, _, _) => handler(state, evnt));

    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, CancellationToken, Task<IImmutableList<object>>> handler) =>
        setup.TransformWith((_, evnt, _, token) => handler(evnt, token));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, CancellationToken, Task<IImmutableList<object>>> handler) =>
        setup.TransformWith((state, evnt, _, token) => handler(state, evnt, token));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, Task<IImmutableList<object>>> handler) =>
        setup.TransformWith((_, evnt, metadata, _) => handler(evnt, metadata));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, Task<IImmutableList<object>>> handler) =>
        setup.TransformWith((state, evnt, metadata, _) => handler(state, evnt, metadata));

    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task<IImmutableList<object>>> handler)
        => setup.TransformWith((_, evnt, metadata, token) => handler(evnt, metadata, token));
    
    public static ISetupStatefulEventReactorFor<TEvent, TState> TransformWith<TEvent, TState>(
        this ISetupStatefulEventReactorFor<TEvent, TState> setup,
        Func<TState?, TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task<IImmutableList<object>>> handler) 
        => setup
            .HandleWith((context, token) => handler(context.State, (TEvent)context.Event, context.Metadata, token));
}