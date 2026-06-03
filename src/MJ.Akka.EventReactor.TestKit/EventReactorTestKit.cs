using FluentAssertions;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Configuration;
using MJ.Akka.EventReactor.Setup;
using Xunit;

namespace MJ.Akka.EventReactor.TestKit;

[PublicAPI]
public abstract class EventReactorTestKit : global::Akka.TestKit.Xunit2.TestKit, IAsyncLifetime
{
    private TestEventReactor _eventReactor = null!;
    
    protected virtual TimeSpan Timeout => TimeSpan.FromSeconds(10);
    protected abstract IConfigureEventReactor GetEventReactorToTest();
    protected virtual Task Setup() => Task.CompletedTask;
    protected abstract IEnumerable<object> When();
    protected virtual Task Then() => Task.CompletedTask;

    protected virtual IHaveConfiguration<EventReactorInstanceConfig> ConfigureReactor(
        IHaveConfiguration<EventReactorInstanceConfig> setup)
    {
        return setup;
    }

    protected void ShouldHaveAckedMessages<T>(
        Func<T, bool>? predicate = null,
        int numberOfMessages = 1)
    {
        _eventReactor
            .AckedMessages
            .OfType<T>()
            .Where(m => predicate == null || predicate(m))
            .Should()
            .HaveCount(numberOfMessages);
    }

    protected void ShouldHaveNackedMessages<T>(
        Func<T, Exception, bool>? predicate = null,
        int numberOfMessages = 1)
    {
        _eventReactor
            .NackedMessages
            .Where(x => x.message is T message && (predicate == null || predicate(message, x.exception)))
            .Should()
            .HaveCount(numberOfMessages);
    }

    public async Task InitializeAsync()
    {
        await Setup();

        var eventReactor = GetEventReactorToTest();
        
        _eventReactor = new TestEventReactor(eventReactor, When().ToArray());
        
        var coordinator = await Sys
            .EventReactors(config => config
                    .WithReactor(_eventReactor, ConfigureReactor))
            .Start();

        var proxy = coordinator.Get(_eventReactor.Name)!;

        await proxy.WaitForCompletion(Timeout);

        await Then();
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}