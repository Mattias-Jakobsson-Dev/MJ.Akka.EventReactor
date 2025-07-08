using Akka.Streams;
using JetBrains.Annotations;
using MJ.Akka.EventReactor.Setup;

namespace MJ.Akka.EventReactor.Configuration;

[PublicAPI]
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
    
    public static IHaveConfiguration<TConfig> WithOutputWriter<TConfig>(
        this IHaveConfiguration<TConfig> haveConfiguration,
        IOutputWriter outputWriter)
        where TConfig : EventReactorConfig
    {
        return haveConfiguration.WithModifiedConfig(config => config with
        {
            OutputWriters = config.OutputWriters.Add(outputWriter)
        });
    }
}