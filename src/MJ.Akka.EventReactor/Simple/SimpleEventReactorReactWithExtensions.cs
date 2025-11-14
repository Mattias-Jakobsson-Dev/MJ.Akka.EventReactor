using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Simple;

[PublicAPI]
public static class SimpleEventReactorReactWithExtensions
{
    public static ISetupSimpleEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Action<TEvent> handler) => setup.ReactWith(evnt =>
    {
        handler(evnt);

        return Task.CompletedTask;
    });
    
    public static ISetupSimpleEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Action<TEvent, IImmutableDictionary<string, object?>> handler) => setup.ReactWith((evnt, metadata) =>
    {
        handler(evnt, metadata);

        return Task.CompletedTask;
    });
    
    public static ISetupSimpleEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, Task> handler) => setup.ReactWith((evnt, _, _) => handler(evnt));

    public static ISetupSimpleEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, Task> handler) => 
        setup.ReactWith((evnt, metadata, _) => handler(evnt, metadata));
    
    public static ISetupSimpleEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, CancellationToken, Task> handler) => setup.ReactWith((evnt, _, cancellationToken) 
        => handler(evnt, cancellationToken));
    
    public static ISetupSimpleEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupSimpleEventReactorFor<TEvent> setup,
        Func<TEvent, IImmutableDictionary<string, object?>, CancellationToken, Task> handler)
    {
        return setup
            .HandleWith(async (context, token) =>
            {
                await handler((TEvent)context.Event, context.Metadata, token);

                return ImmutableList<object>.Empty;
            });
    }
}