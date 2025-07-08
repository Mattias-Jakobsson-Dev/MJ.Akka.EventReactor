using Akka.Streams;
using MJ.Akka.EventReactor.Setup;

namespace MJ.Akka.EventReactor.Configuration;

public static class EventReactorConfigExtensions
{
    public static IHaveConfiguration<TConfig> WithRestartSettings<TConfig>(
        this IHaveConfiguration<TConfig> haveConfiguration,
        RestartSettings? restartSettings)
        where TConfig : EventReactorConfig
    {
        return haveConfiguration.WithModifiedConfig(config => config with
        {
            RestartSettings = restartSettings
        });
    }
    
    public static IHaveConfiguration<TConfig> WithParallelism<TConfig>(
        this IHaveConfiguration<TConfig> haveConfiguration,
        int? parallelism)
        where TConfig : EventReactorConfig
    {
        return haveConfiguration.WithModifiedConfig(config => config with
        {
            Parallelism = parallelism
        });
    }
}