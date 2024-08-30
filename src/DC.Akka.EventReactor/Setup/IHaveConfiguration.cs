using Akka.Actor;

namespace DC.Akka.EventReactor.Setup;

public interface IHaveConfiguration<T> where T : EventReactorConfig
{
    ActorSystem ActorSystem { get; }
    internal T Config { get; }

    internal IHaveConfiguration<T> WithModifiedConfig(Func<T, T> modify);
}