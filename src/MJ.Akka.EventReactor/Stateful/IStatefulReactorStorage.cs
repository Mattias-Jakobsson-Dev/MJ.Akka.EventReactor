namespace MJ.Akka.EventReactor.Stateful;

public interface IStatefulReactorStorage
{
    Task<TState?> Load<TState>(string reactorName, string id, CancellationToken cancellationToken);

    Task Save<TState>(string reactorName, string id, TState state, CancellationToken cancellationToken);

    Task Delete(string reactorName, string id, CancellationToken cancellationToken);
}