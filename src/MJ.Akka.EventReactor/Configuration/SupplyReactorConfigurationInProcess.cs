namespace MJ.Akka.EventReactor.Configuration;

public class SupplyReactorConfigurationInProcess(EventReactorConfiguration config) : ISupplyReactorConfiguration
{
    public EventReactorConfiguration GetConfiguration()
    {
        return config;
    }
}