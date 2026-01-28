using FluentAssertions;
using FluentAssertions.Collections;
using JetBrains.Annotations;

namespace MJ.Akka.EventReactor.Tests;

[PublicAPI]
public static class StoredEventsInterceptorExtensions
{
    public static AndConstraint<GenericCollectionAssertions<StoredEventsInterceptor.StoredEvent>> HaveEvents<T>(
        this GenericCollectionAssertions<StoredEventsInterceptor.StoredEvent> assertions,
        int count = 1,
        Func<T, bool>? predicate = null)
    {
        predicate ??= _ => true;

        return assertions
            .Match(events => events
                .Select(x => x.Event)
                .OfType<T>()
                .Where(predicate)
                .Count() == count);
    }
}