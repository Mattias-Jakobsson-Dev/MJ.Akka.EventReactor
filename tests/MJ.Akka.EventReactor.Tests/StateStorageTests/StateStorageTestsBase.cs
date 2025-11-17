using FluentAssertions;
using MJ.Akka.EventReactor.Stateful;
using Xunit;

namespace MJ.Akka.EventReactor.Tests.StateStorageTests;

public abstract class StateStorageTestsBase
{
    [Fact]
    public async Task Can_save_and_then_load_correct_state()
    {
        var storage = CreateStorage();

        var id = Guid.NewGuid().ToString();

        await storage.Save("test", id, new TestState("test"), CancellationToken.None);

        var loadedState = await storage.Load<TestState>("test", id, CancellationToken.None);

        loadedState.Should().NotBeNull();
        loadedState!.Title.Should().Be("test");
    }

    [Fact]
    public async Task Can_save_and_then_delete_state()
    {
        var storage = CreateStorage();

        var id = Guid.NewGuid().ToString();

        await storage.Save("test", id, new TestState("test"), CancellationToken.None);

        var loadedState = await storage.Load<TestState>("test", id, CancellationToken.None);
        
        loadedState.Should().NotBeNull();

        await storage.Delete("test", id, CancellationToken.None);
        
        loadedState = await storage.Load<TestState>("test", id, CancellationToken.None);

        loadedState.Should().BeNull();
    }

    [Fact]
    public async Task Cant_load_same_id_with_different_reactor_name()
    {
        var storage = CreateStorage();

        var id = Guid.NewGuid().ToString();

        await storage.Save("test1", id, new TestState("test"), CancellationToken.None);
        
        var loadedState = await storage.Load<TestState>("test2", id, CancellationToken.None);

        loadedState.Should().BeNull();
    }
    
    protected abstract IStatefulReactorStorage CreateStorage();

    public record TestState(string Title);
}