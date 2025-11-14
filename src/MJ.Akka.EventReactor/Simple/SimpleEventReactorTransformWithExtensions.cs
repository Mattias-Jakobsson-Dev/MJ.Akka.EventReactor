using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Simple;

[PublicAPI]
public static class SimpleEventReactorTransformWithExtensions
{
    public static ISetupSimpleEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, IImmutableList<object>> handler) => setup.TransformWith(evnt => Task.FromResult(handler(evnt)));
    
    public static ISetupSimpleEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, IImmutableList<object>> handler) => 
        setup.TransformWith((evnt, metadata, _) => Task.FromResult(handler(evnt, metadata)));
    
    public static ISetupSimpleEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, Task<IImmutableList<object>>> handler) => setup.TransformWith((evnt, _, _) => handler(evnt));

    public static ISetupSimpleEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, CancellationToken, Task<IImmutableList<object>>> handler) =>
        setup.TransformWith((evnt, _, token) => handler(evnt, token));
    
    public static ISetupSimpleEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, Task<IImmutableList<object>>> handler) =>
        setup.TransformWith((evnt, metadata, _) => handler(evnt, metadata));
    
    public static ISetupSimpleEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task<IImmutableList<object>>> handler) 
        => setup
            .HandleWith((context, token) => handler((TEvent)context.Event, context.Metadata, token));
}