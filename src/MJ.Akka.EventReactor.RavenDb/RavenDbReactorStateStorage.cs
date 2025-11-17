using MJ.Akka.EventReactor.Stateful;
using Raven.Client.Documents;

namespace MJ.Akka.EventReactor.RavenDb;

public class RavenDbReactorStateStorage(IDocumentStore documentStore) : IStatefulReactorStorage
{
    public async Task<TState?> Load<TState>(string reactorName, string id, CancellationToken cancellationToken)
    {
        using var session = documentStore.OpenAsyncSession();

        var data = await session.LoadAsync<EventReactorState>(
            EventReactorState.BuildId(reactorName, id), cancellationToken);

        if (data == null)
            return default;

        return (TState?)data.State;
    }

    public async Task Save<TState>(string reactorName, string id, TState state, CancellationToken cancellationToken)
    {
        var session = documentStore.OpenAsyncSession();

        await session.StoreAsync(new EventReactorState(
                EventReactorState.BuildId(reactorName, id),
                reactorName,
                id,
                state!),
            cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(string reactorName, string id, CancellationToken cancellationToken)
    {
        using var session = documentStore.OpenAsyncSession();
        
        session.Delete(EventReactorState.BuildId(reactorName, id));

        await session.SaveChangesAsync(cancellationToken);
    }
}