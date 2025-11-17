namespace MJ.Akka.EventReactor.Stateful;

public interface IStatefulReactorStorage
{
    Task<TState?> Load<TState>(string id, CancellationToken cancellationToken);

    Task Save<TState>(string id, TState state, CancellationToken cancellationToken);

    Task Delete(string id, CancellationToken cancellationToken);
}