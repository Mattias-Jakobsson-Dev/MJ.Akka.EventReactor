using Akka;
using Akka.Streams.Dsl;

namespace DC.Akka.EventReactor;

public interface IEventReactor
{
    string Name { get; }

    void Configure(Func<ISetupEventReactor, ISetupEventReactor> config);

    Source<IMessageWithAck, NotUsed> StartSource();
}