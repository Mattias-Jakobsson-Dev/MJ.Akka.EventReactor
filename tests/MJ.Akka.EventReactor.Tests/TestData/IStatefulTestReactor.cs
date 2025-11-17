using MJ.Akka.EventReactor.Stateful;

namespace MJ.Akka.EventReactor.Tests.TestData;

public interface IStatefulTestReactor : ITestReactor
{
    IStatefulReactorStorage GetStorage();
}