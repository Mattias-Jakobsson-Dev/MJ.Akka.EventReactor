using System.Collections.Concurrent;
using System.Collections.Immutable;
using Akka;
using Akka.Streams.Dsl;
using DC.Akka.EventReactor.PositionStreamSource;
using DC.Akka.EventReactor.Tests.TestData;
using Xunit;

namespace DC.Akka.EventReactor.Tests.EventReactorCoordinatorTests;

public class PositionedStreamReactorTests(NormalTestKitActorSystem systemHandler) 
    : EventReactorCoordinatorTestsBase(systemHandler), IClassFixture<NormalTestKitActorSystem>
{
    protected override ITestReactor CreateReactor(IImmutableList<Events.IEvent> events)
    {
        return new PositionedStreamReactor(events);
    }
    
    private class PositionedStreamReactor(IImmutableList<Events.IEvent> events) 
        : EventReactorWithPositionedStream, ITestReactor
    {
        private readonly ConcurrentDictionary<string, int> _handledEvents = [];
        
        public override string Name => GetType().Name;
        
        public override ISetupEventReactor Configure(ISetupEventReactor config)
        {
            return TestReactor.ConfigureHandlers(config, _handledEvents);
        }

        protected override IStartPositionStream GetStreamSource()
        {
            return new PositionStreamStarter(events);
        }

        public IImmutableDictionary<string, int> GetHandledEvents()
        {
            return _handledEvents.ToImmutableDictionary();
        }
        
        private class PositionStreamStarter(IImmutableList<Events.IEvent> events) : IStartPositionStream
        {
            public Source<EventWithPosition, NotUsed> StartFrom(long? position)
            {
                return Source
                    .From(events.Select((evnt, index) => new EventWithPosition(evnt, index + 1)))
                    .Where(x => position == null || x.Position > position);
            }
        }
    }
}