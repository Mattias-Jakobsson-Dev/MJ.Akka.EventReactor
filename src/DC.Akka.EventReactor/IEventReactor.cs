using Akka;
using Akka.Streams.Dsl;

namespace DC.Akka.EventReactor;

public interface IEventReactor
{
    string Name { get; }

    ISetupEventReactor Configure(ISetupEventReactor config);

    Source<IMessageWithAck, NotUsed> StartSource();
}