using Akka.Actor;
using MJ.Akka.EventReactor.Configuration;

namespace MJ.Akka.EventReactor.Setup;

public interface IHaveConfiguration<T> where T : EventReactorConfig
{
    ActorSystem ActorSystem { get; }
    internal T Config { get; }

    internal IHaveConfiguration<T> WithModifiedConfig(Func<T, T> modify);
}