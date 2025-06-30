using System.Collections.Immutable;
using Akka.Actor;

namespace MJ.Akka.EventReactor.Configuration;

public class ReactorConfigurationsSupplier : IExtension
{
    private readonly IImmutableDictionary<string, EventReactorConfiguration> _configuredProjections;

    private ReactorConfigurationsSupplier(
        IImmutableDictionary<string, EventReactorConfiguration> configuredProjections)
    {
        _configuredProjections = configuredProjections;
    }

    public EventReactorConfiguration GetConfigurationFor(string projectionName)
    {
        return _configuredProjections.TryGetValue(projectionName, out var config)
            ? config
            : throw new NoEventReactorException(projectionName);
    }

    internal static void Register(
        ActorSystem actorSystem,
        IImmutableDictionary<string, EventReactorConfiguration> projectionConfigurations)
    {
        actorSystem.RegisterExtension(new Provider(new ReactorConfigurationsSupplier(projectionConfigurations)));
    }

    private class Provider(ReactorConfigurationsSupplier supplier)
        : ExtensionIdProvider<ReactorConfigurationsSupplier>
    {
        public override ReactorConfigurationsSupplier CreateExtension(ExtendedActorSystem system)
        {
            return supplier;
        }
    }
}