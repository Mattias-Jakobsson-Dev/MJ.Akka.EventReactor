using System.Collections.Immutable;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Setup;

[PublicAPI]
public static class SetupEventReactorForExtensions
{
    public static ISetupEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupEventReactorFor<TEvent> setup,
        Action<TEvent> handler) => setup.ReactWith(evnt =>
    {
        handler(evnt);

        return Task.CompletedTask;
    });
    
    public static ISetupEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupEventReactorFor<TEvent> setup,
        Func<TEvent, Task> handler) => setup.ReactWith((evnt, _) => handler(evnt));
    
    public static ISetupEventReactorFor<TEvent> ReactWith<TEvent>(
        this ISetupEventReactorFor<TEvent> setup,
        Func<TEvent, CancellationToken, Task> handler)
    {
        return setup
            .HandleWith(async (evnt, token) =>
            {
                await handler(evnt, token);

                return ImmutableList<object>.Empty;
            });
    }
    
    public static ISetupEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupEventReactorFor<TEvent> setup,
        Func<TEvent, IImmutableList<object>> handler) => setup.TransformWith(evnt => Task.FromResult(handler(evnt)));
    
    public static ISetupEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupEventReactorFor<TEvent> setup,
        Func<TEvent, Task<IImmutableList<object>>> handler) => setup.TransformWith((evnt, _) => handler(evnt));

    public static ISetupEventReactorFor<TEvent> TransformWith<TEvent>(
        this ISetupEventReactorFor<TEvent> setup,
        Func<TEvent, CancellationToken, Task<IImmutableList<object>>> handler) => setup.HandleWith(handler);
}