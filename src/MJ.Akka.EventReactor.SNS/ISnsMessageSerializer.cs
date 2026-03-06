namespace MJ.Akka.EventReactor.SNS;

public interface ISnsMessageSerializer
{
    Task<string> Serialize(object message);
}